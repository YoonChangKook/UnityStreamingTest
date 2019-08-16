using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using FFmpeg.AutoGen;

public class Tester : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("current directory: " + Environment.CurrentDirectory);
        Debug.Log("platform: " + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == true ? "Windows" : "Other"));
        RegisterFFmpegBinaries();
        Debug.Log(ffmpeg.av_version_info());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

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
