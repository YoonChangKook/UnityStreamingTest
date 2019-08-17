using System;
using FFmpeg.AutoGen;

/// <summary>
/// FFmpeg 라이브러리를 이용한 RTP Video Streamer
/// 
/// MPEG2 코덱을 기본으로 사용한다.
/// </summary>
public unsafe class RtpVideoStreamer : IDisposable
{
    private static readonly AVCodecID DEFAULT_VIDEO_CODEC = AVCodecID.AV_CODEC_ID_MPEG1VIDEO;
    private static readonly AVPixelFormat DEFAULT_SRC_PIXEL_FORMAT = AVPixelFormat.AV_PIX_FMT_YUV420P;
    private static readonly AVPixelFormat DEFAULT_DST_PIXEL_FORMAT = AVPixelFormat.AV_PIX_FMT_YUV420P;
    private static readonly long DEFAULT_BIT_RATE = 1600000L;
    public static readonly int VIDEO_FPS = 30;

    private readonly int videoWidth;
    private readonly int videoHeight;
    private readonly AVFormatContext* _formatContext;
    private readonly AVCodecContext* _codecContext;
    private readonly AVStream* _avStream;
    private readonly AVFrame* _frame;
    private readonly AVPacket* _packet;
    private readonly SwsContext* _convertContext;
    private long _frameIndex;

    public RtpVideoStreamer(string rtpUrl, int width = 1920, int height = 1080)
    {
        this.videoWidth = width;
        this.videoHeight = height;

        // RTP Output Context 할당
        AVFormatContext* formatContext;
        ffmpeg.avformat_alloc_output_context2(&formatContext, null, "rtp", rtpUrl);
        this._formatContext = formatContext;

        // RTP 코덱 찾기
        AVCodec* avCodec = ffmpeg.avcodec_find_encoder(DEFAULT_VIDEO_CODEC);
        this._formatContext->video_codec = avCodec;
        // RTP 스트림 생성
        this._avStream = ffmpeg.avformat_new_stream(this._formatContext, avCodec);
        this._avStream->id = (int)this._formatContext->nb_streams - 1;

        // 코텍 초기화
        this._codecContext = _avStream->codec;

        // 컨텍스트 초기화
        InitContext(rtpUrl);

        // RTP 비디오 오픈
        ffmpeg.avcodec_open2(this._codecContext, avCodec, null);
        ffmpeg.av_dump_format(this._formatContext, 0, rtpUrl, 1);

        // 프레임, 패킷 할당
        this._frame = InitFrame(DEFAULT_DST_PIXEL_FORMAT);
        this._frameIndex = 0;
        this._packet = ffmpeg.av_packet_alloc();

        // 프레임 픽셀 포멧 변환 컨텍스트 할당
        this._convertContext = ffmpeg.sws_getContext(
            this._codecContext->width, this._codecContext->height, DEFAULT_SRC_PIXEL_FORMAT,
            this._codecContext->width, this._codecContext->height, this._codecContext->pix_fmt,
            ffmpeg.SWS_BICUBIC, null, null, null);
    }

    /// <summary>
    /// 객체가 소멸될 때 호출되는 메서드
    /// 스트리밍 관련 리소스를 해제한다.
    /// </summary>
    public void Dispose()
    {
        ffmpeg.av_frame_unref(this._frame);
        ffmpeg.av_free(this._frame);

        ffmpeg.av_packet_unref(this._packet);
        ffmpeg.av_free(this._packet);

        ffmpeg.avcodec_close(_codecContext);
        var pFormatContext = _formatContext;
        ffmpeg.avformat_close_input(&pFormatContext);

        ffmpeg.sws_freeContext(this._convertContext);
    }

    /// <summary>
    /// 컨텍스트의 초기값을 설정한다.
    /// </summary>
    private void InitContext(string rtpUrl)
    {
        this._codecContext->codec_id = DEFAULT_VIDEO_CODEC;
        this._codecContext->width = this.videoWidth;
        this._codecContext->height = this.videoHeight;
        this._codecContext->bit_rate = DEFAULT_BIT_RATE;
        this._codecContext->time_base.den = VIDEO_FPS;
        this._codecContext->time_base.num = 1;
        this._codecContext->framerate.den = 1;
        this._codecContext->framerate.num = VIDEO_FPS;
        this._codecContext->gop_size = 12;
        this._codecContext->pix_fmt = DEFAULT_DST_PIXEL_FORMAT;

        if ((this._formatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
            this._codecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

        if ((this._formatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
        {
            ffmpeg.avio_open(&this._formatContext->pb, rtpUrl, ffmpeg.AVIO_FLAG_WRITE);
        }

        ffmpeg.avformat_write_header(this._formatContext, null);
    }

    /// <summary>
    /// 지정된 포멧으로 프레임을 할당하여 반환한다.
    /// </summary>
    /// <param name="pixelFormat">프레임의 픽셀 포멧 (ex: RGB24)</param>
    /// <returns>할당된 프레임</returns>
    private AVFrame* InitFrame(AVPixelFormat pixelFormat)
    {
        AVFrame* frame = ffmpeg.av_frame_alloc();
        frame->format = (int)pixelFormat;
        frame->width = this._codecContext->width;
        frame->height = this._codecContext->height;
        ffmpeg.av_frame_get_buffer(frame, 32);

        return frame;
    }

    public void WriteFrame(AVFrame* srcFrame)
    {
        // 프레임을 쓰기 가능으로 만들기
        ffmpeg.av_frame_make_writable(this._frame);

        // 픽셀 포멧 변환
        ffmpeg.sws_scale(this._convertContext,
            srcFrame->data, srcFrame->linesize, 0, this.videoHeight,
            this._frame->data, this._frame->linesize);

        // 프레임 인덱스 설정
        this._frame->pts = this._frameIndex++;

        // 인코딩
        ffmpeg.avcodec_send_frame(this._codecContext, this._frame);
        ffmpeg.avcodec_receive_packet(this._codecContext, this._packet);

        // 스트림으로 전송
        ffmpeg.av_packet_rescale_ts(this._packet, this._codecContext->time_base, this._avStream->time_base);
        this._packet->stream_index = this._avStream->index;
        ffmpeg.av_write_frame(this._formatContext, this._packet);

        ffmpeg.av_packet_unref(this._packet);
    }
}
