using System;

namespace AudioBridge.Desktop.Capture;

public interface IAudioCapture
{
    event EventHandler<byte[]> DataAvailable;
    event EventHandler<Exception> ErrorOccurred;
    event EventHandler<bool> IsCapturingChanged;

    bool IsCapturing { get; }
    int SampleRate { get; }
    int Channels { get; }
    int BitsPerSample { get; }

    void Start();
    void Stop();
}
