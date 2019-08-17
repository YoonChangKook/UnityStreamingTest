using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using FFmpeg.AutoGen;

public unsafe class TestStreamer : MonoBehaviour
{
    private RtpVideoStreamer streamer;
    private AVFrame* srcFrame;
    private int circularColor;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("current directory: " + Environment.CurrentDirectory);
        Debug.Log("platform: " + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == true ? "Windows" : "Other"));
        RegisterFFmpegBinaries();
        Debug.Log(ffmpeg.av_version_info());
        this.streamer = new RtpVideoStreamer("rtp://127.0.0.1:9000/test/");
        this.circularColor = 0;
        this.srcFrame = ffmpeg.av_frame_alloc();
    }

    // Update is called once per frame
    void Update()
    {
        srcFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
        srcFrame->width = 1920;
        srcFrame->height = 1080;

        ffmpeg.av_frame_get_buffer(srcFrame, 32);

        // 더미 이미지 만들기
        /* Y */
        for (int y = 0; y < 1080; y++)
        {
            for (int x = 0; x < 1920; x++)
            {
                srcFrame->data[0][y * srcFrame->linesize[0] + x] = (byte)(x + y + circularColor * 3);
            }
        }

        /* Cb and Cr */
        for (int y = 0; y < 1080 / 2; y++)
        {
            for (int x = 0; x < 1920 / 2; x++)
            {
                srcFrame->data[1][y * srcFrame->linesize[1] + x] = (byte)(128 + y + circularColor * 2);
                srcFrame->data[2][y * srcFrame->linesize[2] + x] = (byte)(64 + x + circularColor * 5);
            }
        }

        // 더미 이미지 송신
        streamer.WriteFrame(srcFrame);

        circularColor = (byte)(circularColor + 1);
        Thread.Sleep(1000 / RtpVideoStreamer.VIDEO_FPS);
    }

    /// <summary>
    /// FFmpeg 바이너리를 사용할 수 있도록 Root Path를 등록해주는 메서드
    /// </summary>
    private void RegisterFFmpegBinaries()
    {
        var current = Environment.CurrentDirectory;
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
}
