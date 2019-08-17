using System;
using System.IO;
using UnityEngine;
using FFmpeg.AutoGen;

public unsafe class TestReceiver : MonoBehaviour
{
    public GameObject streamingViewer;

    private RtpVideoReceiver receiver;
    private Texture2D texture;

    // Start is called before the first frame update
    void Start()
    {
        RegisterFFmpegBinaries();
        this.receiver = new RtpVideoReceiver("rtp://127.0.0.1:9000/test/");
        this.texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
    }

    // Update is called once per frame
    void Update()
    {
        AVFrame receivedFrame = receiver.ReceiveFrame();

        texture.LoadRawTextureData((IntPtr)receivedFrame.data[0], 1920 * 1080 * 3);
        texture.Apply();

        streamingViewer.GetComponent<Renderer>().material.mainTexture = texture;
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
