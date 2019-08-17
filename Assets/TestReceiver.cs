using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using FFmpeg.AutoGen;

public unsafe class TestReceiver : MonoBehaviour
{
    public Text logText;
    public GameObject streamingViewer;

    private RtpVideoReceiver receiver;
    private Texture2D texture;

    // Start is called before the first frame update
    void Start()
    {
        this.logText.text += "Test Started.\n";
        RegisterFFmpegBinaries();
        this.receiver = new RtpVideoReceiver("rtp://127.0.0.1:9000/test/");
        this.texture = new Texture2D(this.receiver.VideoWidth, this.receiver.VideoHeight, TextureFormat.RGB24, false);
    }

    // Update is called once per frame
    void Update()
    {
        AVFrame receivedFrame = receiver.ReceiveFrame();
        this.logText.text = "Received!";

        texture.LoadRawTextureData((IntPtr)receivedFrame.data[0], this.receiver.VideoWidth * this.receiver.VideoHeight * 3);
        texture.Apply();

        streamingViewer.GetComponent<Renderer>().material.mainTexture = texture;
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
                this.logText.text += $"FFmpeg binaries found in: " + ffmpegBinaryPath + "\n";
                ffmpeg.RootPath = ffmpegBinaryPath;
                return;
            }

            current = Directory.GetParent(current)?.FullName;
        }
    }
}
