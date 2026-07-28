#pragma warning disable CS0067 // unused events (required by IAudioCapture)

using System;

namespace AudioBridge.Desktop.Capture;

/// <summary>
/// TODO: Implementazione Linux per cattura audio loopback via PipeWire (pw-record).
/// 
/// Steps previsti:
///   1. Avviare processo pw-record come sottoprocesso con argomenti:
///      pw-record --target=@DEFAULT_MONITOR@ --format=s16 --rate=48000 --channels=2 --latency=20ms -
///   2. Leggere lo stdout del processo (WAV/PCM raw).
///   3. Bufferizzare e convertire in float[] (o short[]) per coerenza con IAudioCapture.
///   4. Gestire riavvio su crash del processo, cleanup su stop/dispose.
/// 
/// Non implementato: sviluppo futuro dopo Windows + Android funzionanti.
/// </summary>
public sealed class LinuxPipeWireCapture : IAudioCapture, IDisposable
{
    public event EventHandler<byte[]>? DataAvailable;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<bool>? IsCapturingChanged;

    public bool IsCapturing => false;
    public int SampleRate => 48000;
    public int Channels => 2;
    public int BitsPerSample => 16;

    public void Start()
    {
        // TODO: implementare cattura Linux via pw-record
        Console.WriteLine("[AudioBridge] LinuxPipeWireCapture: cattura non implementata.");
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
