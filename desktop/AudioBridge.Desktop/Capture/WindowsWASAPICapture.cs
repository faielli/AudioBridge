using NAudio.Wave;
using System;

namespace AudioBridge.Desktop.Capture;

public sealed class WindowsWASAPICapture : IAudioCapture, IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private volatile bool _isCapturing;
    private bool _stopRequested;

    public event EventHandler<byte[]>? DataAvailable;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<bool>? IsCapturingChanged;

    public bool IsCapturing => _isCapturing;
    public int SampleRate => _capture?.WaveFormat?.SampleRate ?? 48000;
    public int Channels => _capture?.WaveFormat?.Channels ?? 2;
    public int BitsPerSample => _capture?.WaveFormat?.BitsPerSample ?? 32;

    public void Start()
    {
        Console.WriteLine($"[DEBUG] WASAPICapture.Start() — _isCapturing={_isCapturing} _stopRequested={_stopRequested}");
        if (_isCapturing)
        {
            Console.WriteLine($"[DEBUG] WASAPICapture.Start() — già in cattura, skip");
            return;
        }

        try
        {
            _stopRequested = false;
            Console.WriteLine($"[DEBUG] WASAPICapture.Start() — creazione nuovo WasapiLoopbackCapture...");
            _capture = new WasapiLoopbackCapture();
            Console.WriteLine($"[DEBUG] WASAPICapture.Start() — WasapiLoopbackCapture creato, SR={_capture.WaveFormat?.SampleRate} CH={_capture.WaveFormat?.Channels}");
            _capture.DataAvailable += OnDataCaptured;
            _capture.RecordingStopped += OnRecordingStopped;
            Console.WriteLine($"[DEBUG] WASAPICapture.Start() — chiamata StartRecording()...");
            _capture.StartRecording();
            _isCapturing = true;
            IsCapturingChanged?.Invoke(this, true);
            Console.WriteLine($"[DEBUG] WASAPICapture.Start() — completato, _isCapturing=true");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] WASAPICapture.Start() — ERRORE: {ex.GetType().Name}: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private int _waveEventCount;
    private void OnDataCaptured(object? sender, WaveInEventArgs e)
    {
        _waveEventCount++;
        if (_waveEventCount <= 5 || _waveEventCount % 100 == 0)
            Console.WriteLine($"[DEBUG] WASAPICapture.OnDataCaptured #{_waveEventCount} — BytesRecorded={e.BytesRecorded} _isCapturing={_isCapturing}");

        if (e.BytesRecorded > 0)
        {
            var data = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, data, 0, e.BytesRecorded);
            DataAvailable?.Invoke(this, data);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        Console.WriteLine($"[DEBUG] WASAPICapture.OnRecordingStopped — _isCapturing={_isCapturing} _stopRequested={_stopRequested} error={e.Exception?.Message}");

        var wasCapturing = _isCapturing;
        _isCapturing = false;

        if (wasCapturing && !_stopRequested)
        {
            Console.WriteLine($"[DEBUG] WASAPICapture.OnRecordingStopped — cattura terminata inaspettatamente!");
            ErrorOccurred?.Invoke(this, e.Exception ?? new Exception("Cattura audio terminata inaspettatamente"));
        }

        IsCapturingChanged?.Invoke(this, false);
        Console.WriteLine($"[DEBUG] WASAPICapture.OnRecordingStopped — fine");
    }

    public void Stop()
    {
        Console.WriteLine($"[DEBUG] WASAPICapture.Stop() — _isCapturing={_isCapturing} _capture={_capture?.GetHashCode()}");
        _stopRequested = true;
        _isCapturing = false;
        if (_capture != null)
        {
            Console.WriteLine($"[DEBUG] WASAPICapture.Stop() — chiamata StopRecording()...");
            try { _capture.StopRecording(); } catch (Exception ex) { Console.WriteLine($"[DEBUG] WASAPICapture.Stop() — StopRecording eccezione: {ex.Message}"); }
            Console.WriteLine($"[DEBUG] WASAPICapture.Stop() — pulizia eventi e dispose...");
            _capture.DataAvailable -= OnDataCaptured;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
            Console.WriteLine($"[DEBUG] WASAPICapture.Stop() — completato");
        }
        else
        {
            Console.WriteLine($"[DEBUG] WASAPICapture.Stop() — _capture già null, no-op");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
