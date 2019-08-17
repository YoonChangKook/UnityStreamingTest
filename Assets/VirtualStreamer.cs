using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using FFmpeg.AutoGen;

[RequireComponent(typeof(Camera))]
public unsafe class VirtualStreamer : MonoBehaviour, IDisposable
{
    // Public Properties
    public int maxFrames; // maximum number of frames you want to record in one video
    public int frameRate = 30; // number of frames to capture per second

    // The Encoder Thread
    private Thread encoderThread;

    // Texture Readback Objects
    private RenderTexture tempRenderTexture;
    private Texture2D tempTexture2D;

    // Timing Data
    private float captureFrameTime;
    private float lastFrameTime;
    private int frameNumber;

    // Encoder Thread Shared Resources
    private Queue<byte[]> frameQueue;
    private int screenWidth;
    private int screenHeight;
    private bool threadIsProcessing;
    private bool terminateThreadWhenDone;

    // Streaming
    private RtpVideoStreamer streamer;
    private AVFrame* srcFrame;
    private SwsContext* _convertContext;
    private IntPtr _convertedFrameBufferPtr;
    private byte_ptrArray4 _convertDstData;
    private int_array4 _convertDstLinesize;

    public void Dispose()
    {
        ffmpeg.av_frame_unref(this.srcFrame);
        ffmpeg.av_free(this.srcFrame);

        Marshal.FreeHGlobal(_convertedFrameBufferPtr);
        ffmpeg.sws_freeContext(this._convertContext);
    }

    // Start is called before the first frame update
    void Start()
    {
        RegisterFFmpegBinaries();

        // Prepare textures and initial values
        screenWidth = GetComponent<Camera>().pixelWidth;
        screenHeight = GetComponent<Camera>().pixelHeight;
        Debug.Log("Width: " + screenWidth + ", Height: " + screenHeight);

        // RTP 스트림 할당
        this.streamer = new RtpVideoStreamer("rtp://127.0.0.1:9000/test/", screenWidth, screenHeight);
        // 송신할 프레임 할당
        this.srcFrame = ffmpeg.av_frame_alloc();
        this.srcFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
        this.srcFrame->width = screenWidth;
        this.srcFrame->height = screenHeight;
        ffmpeg.av_frame_get_buffer(this.srcFrame, 32);
        // 테스트를 위해 RGB24 to YUV420P 변환 컨텍스트 할당
        this._convertContext = ffmpeg.sws_getContext(
            screenWidth, screenHeight, AVPixelFormat.AV_PIX_FMT_RGB24,
            screenWidth, screenHeight, AVPixelFormat.AV_PIX_FMT_YUV420P,
            ffmpeg.SWS_BICUBIC, null, null, null);

        var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_RGB24, (int)screenWidth, (int)screenHeight, 1);
        this._convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
        this._convertDstData = new byte_ptrArray4();
        this._convertDstLinesize = new int_array4();

        // Set target frame rate (optional)
        Application.targetFrameRate = frameRate;

        tempRenderTexture = new RenderTexture(screenWidth, screenHeight, 0);
        tempTexture2D = new Texture2D(screenWidth, screenHeight, TextureFormat.RGB24, false);
        frameQueue = new Queue<byte[]>();

        frameNumber = 0;

        captureFrameTime = 1.0f / (float)frameRate;
        lastFrameTime = Time.time;

        // Kill the encoder thread if running from a previous execution
        if (encoderThread != null && (threadIsProcessing || encoderThread.IsAlive))
        {
            threadIsProcessing = false;
            encoderThread.Join();
        }

        // Start a new encoder thread
        threadIsProcessing = true;
        encoderThread = new Thread(EncodeAndSave);
        encoderThread.Start();
    }

    /// <summary>
    /// FFmpeg 바이너리를 사용할 수 있도록 Root Path를 등록해주는 메서드
    /// </summary>
    private void RegisterFFmpegBinaries()
    {
        var current = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        var probe = "FFmpeg";
        while (current != null)
        {
            var ffmpegBinaryPath = Path.Combine(current, probe);
            if (Directory.Exists(ffmpegBinaryPath))
            {
                Debug.Log($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                ffmpeg.RootPath = ffmpegBinaryPath;
                return;
            }

            current = Directory.GetParent(current)?.FullName;
        }
    }

    void OnDisable()
    {
        // Reset target frame rate
        Application.targetFrameRate = -1;

        // Inform thread to terminate when finished processing frames
        terminateThreadWhenDone = true;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Check if render target size has changed, if so, terminate
        if (source.width != screenWidth || source.height != screenHeight)
        {
            threadIsProcessing = false;
            this.enabled = false;
            throw new UnityException("ScreenRecorder render target size has changed!");
        }

        // Calculate number of video frames to produce from this game frame
        // Generate 'padding' frames if desired framerate is higher than actual framerate
        float thisFrameTime = Time.time;
        int framesToCapture = ((int)(thisFrameTime / captureFrameTime)) - ((int)(lastFrameTime / captureFrameTime));

        // Capture the frame
        if (framesToCapture > 0)
        {
            Graphics.Blit(source, tempRenderTexture);

            RenderTexture.active = tempRenderTexture;
            tempTexture2D.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            RenderTexture.active = null;
        }

        // Add the required number of copies to the queue
        for (int i = 0; i < framesToCapture; ++i)
        {
            frameQueue.Enqueue(tempTexture2D.GetRawTextureData());

            frameNumber++;

            if (frameNumber % frameRate == 0)
            {
                print("Frame " + frameNumber);
            }
        }

        lastFrameTime = thisFrameTime;

        // Passthrough
        Graphics.Blit(source, destination);
    }

    private void EncodeAndSave()
    {
        print("SCREENRECORDER IO THREAD STARTED");

        while (threadIsProcessing)
        {
            if (frameQueue.Count > 0)
            {
                Debug.Log("Write Frame !!");

                // 이미지 복사
                byte[] rawData = this.frameQueue.Dequeue();
                //rawData = GetFlipedImage(rawData, screenWidth, screenHeight, 3);
                fixed (byte* rawDataPtr = &rawData[0])
                    ffmpeg.av_image_fill_arrays(ref this._convertDstData, ref this._convertDstLinesize,
                        rawDataPtr, AVPixelFormat.AV_PIX_FMT_RGB24, screenWidth, screenHeight, 1);

                // 프레임 픽셀 포멧 변환 (RGB24 -> YUV420P)
                ffmpeg.sws_scale(this._convertContext,
                    this._convertDstData, this._convertDstLinesize, 0, screenHeight,
                    this.srcFrame->data, this.srcFrame->linesize);

                // 프레임 송신
                this.streamer.WriteFrame(this.srcFrame);
            }
            else
            {
                if (terminateThreadWhenDone)
                {
                    break;
                }

                Thread.Sleep(1);
            }
        }

        terminateThreadWhenDone = false;
        threadIsProcessing = false;

        print("SCREENRECORDER IO THREAD FINISHED");
    }

    /// <summary>
    /// 이미지를 상하반전한다.
    /// 
    /// 유니티 UV 좌표계가 반대라서 필요하다.
    /// </summary>
    /// <param name="data">원본 데이터</param>
    /// <param name="width">너비</param>
    /// <param name="height">높이</param>
    /// <param name="colorByte">컬러 당 바이트 수 (RGB: 3)</param>
    /// <returns></returns>
    private byte[] GetFlipedImage(byte[] data, int width, int height, int colorByte = 3)
    {
        byte[] newData = new byte[data.Length];

        for (int i = 0; i < height; i++)
        {
            int originIdx = i * width * colorByte;
            int newIdx = (height - i - 1) * width * colorByte;
            for (int j = 0; j < width * colorByte; j++)
            {
                newData[newIdx + j] = data[originIdx + j];
            }
        }

        return newData;
    }
}
