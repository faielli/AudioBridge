using System.Text.Json.Serialization;

namespace AudioBridge.Desktop.Models;

public class AppSettings
{
    public string AudioDeviceName { get; set; } = "";
    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
    public int Bitrate { get; set; } = 192000;
    public int FrameSizeMs { get; set; } = 20;
    public int SelectedProfile { get; set; } = 0;

    public int UdpPort { get; set; } = 54320;
    public int TcpControlPort { get; set; } = 54321;
    public int NetworkBufferMs { get; set; } = 50;
    public bool MdnsEnabled { get; set; } = true;
    public string ManualIp { get; set; } = "";

    public int JitterBufferFrames { get; set; } = 3;
    public bool AutoStartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;

    [JsonIgnore]
    public bool IsStereo => Channels == 2;

    public void SetStereo(bool stereo) => Channels = stereo ? 2 : 1;

    public static AppSettings CreateDefault() => new();
}
