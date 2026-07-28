namespace AudioBridge.Desktop.Models;

public class AudioDeviceInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public override string ToString() => Description;
}
