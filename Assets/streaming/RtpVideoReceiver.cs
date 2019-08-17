using System;
using FFmpeg.AutoGen;
using System.Runtime.InteropServices;

/// <summary>
/// FFmpeg 라이브러리를 이용한 RTP Video Streamer
/// 
/// 스트림 생성 및 수신 시 Blocking되므로 반드시 고려하여 프로그래밍
/// </summary>
public unsafe class RtpVideoReceiver : IDisposable
{
    private static readonly AVPixelFormat DEFAULT_DST_PIXEL_FORMAT = AVPixelFormat.AV_PIX_FMT_RGB24;
    private static readonly int VIDEO_WIDTH = 1920;
    private static readonly int VIDEO_HEIGHT = 1080;

    private readonly AVCodecContext* _codecContext;
    private readonly AVFormatContext* _formatContext;
    private readonly AVFrame* _frame;
    private readonly AVPacket* _packet;
    private readonly SwsContext* _convertContext;
    private readonly IntPtr _convertedFrameBufferPtr;
    private readonly byte_ptrArray4 _convertDstData;
    private readonly int_array4 _convertDstLinesize;

    public RtpVideoReceiver(string rtpUrl)
    {
        ffmpeg.avformat_network_init();

        // RTP Input Context 할당
        var formatContext = ffmpeg.avformat_alloc_context();
        ffmpeg.avformat_open_input(&formatContext, rtpUrl, null, null);
        this._formatContext = formatContext;

        // RTP 스트림 정보 획득
        ffmpeg.avformat_find_stream_info(this._formatContext, null);

        // RTP 스트림 중 첫번째 비디오 스트림을 가져온다.
        AVStream* avStream = null;
        for (int i = 0; i < this._formatContext->nb_streams; i++)
        {
            if (this._formatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                avStream = this._formatContext->streams[i];
            }
        }
        if (avStream == null) throw new InvalidOperationException("Could not found video stream.");

        // RTP 스트림 코덱 컨텍스트 할당
        this._codecContext = avStream->codec;

        // RTP 디코더 코덱 찾기
        AVCodecID avCodecId = this._codecContext->codec_id;
        AVCodec* avCodec = ffmpeg.avcodec_find_decoder(avCodecId);
        if (avCodec == null) throw new InvalidOperationException("Unsupported Codec");

        // 코덱 오픈
        ffmpeg.avcodec_open2(this._codecContext, avCodec, null);

        // 프레임, 패킷 할당
        this._frame = ffmpeg.av_frame_alloc();
        this._packet = ffmpeg.av_packet_alloc();

        // 프레임 픽셀 포멧 변환 컨텍스트 할당
        this._convertContext = ffmpeg.sws_getContext(
            this._codecContext->width, this._codecContext->height, this._codecContext->pix_fmt,
            this._codecContext->width, this._codecContext->height, DEFAULT_DST_PIXEL_FORMAT,
            ffmpeg.SWS_BICUBIC, null, null, null);
        if (this._convertContext == null) throw new ApplicationException("Could not initialize the conversion context.");

        // 픽셀 포멧 변환용 임시 저장소 할당
        var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(DEFAULT_DST_PIXEL_FORMAT, (int)VIDEO_WIDTH, (int)VIDEO_HEIGHT, 1);
        this._convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
        this._convertDstData = new byte_ptrArray4();
        this._convertDstLinesize = new int_array4();

        ffmpeg.av_image_fill_arrays(ref this._convertDstData, ref this._convertDstLinesize,
            (byte*)this._convertedFrameBufferPtr, DEFAULT_DST_PIXEL_FORMAT, (int)VIDEO_WIDTH, (int)VIDEO_HEIGHT, 1);
    }

    /// <summary>
    /// 객체가 소멸될 때 호출되는 메서드.
    /// 스트리밍 관련 리소스를 해제한다.
    /// </summary>
    public void Dispose()
    {
        ffmpeg.av_frame_unref(_frame);
        ffmpeg.av_free(_frame);

        ffmpeg.av_packet_unref(_packet);
        ffmpeg.av_free(_packet);

        ffmpeg.avcodec_close(_codecContext);
        var pFormatContext = _formatContext;
        ffmpeg.avformat_close_input(&pFormatContext);

        Marshal.FreeHGlobal(_convertedFrameBufferPtr);
        ffmpeg.sws_freeContext(this._convertContext);
    }

    /// <summary>
    /// RTP 스트림으로부터 프레임을 받은 뒤, 픽셀 포멧을 변환하여 반환한다.
    /// 
    /// Blocking 함수
    /// </summary>
    /// <returns>수신받은 프레임</returns>
    public AVFrame ReceiveFrame()
    {
        // 프레임, 패킷 레퍼런스 해제
        ffmpeg.av_frame_unref(this._frame);
        ffmpeg.av_packet_unref(this._packet);

        // 프레임 수신
        ffmpeg.av_read_frame(this._formatContext, this._packet);

        // 프레임 디코딩
        ffmpeg.avcodec_send_packet(this._codecContext, this._packet);
        ffmpeg.avcodec_receive_frame(this._codecContext, this._frame);

        // 프레임 픽셀 포멧 변환 (YUV420P -> RGB24)
        ffmpeg.sws_scale(this._convertContext,
            this._frame->data, this._frame->linesize, 0, this._frame->height,
            this._convertDstData, this._convertDstLinesize);

        var data = new byte_ptrArray8();
        data.UpdateFrom(this._convertDstData);
        var linesize = new int_array8();
        linesize.UpdateFrom(this._convertDstLinesize);

        return new AVFrame
        {
            data = data,
            linesize = linesize,
            width = (int)this._frame->width,
            height = (int)this._frame->height
        };
    }
}
