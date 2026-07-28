using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AudioBridge.Desktop.Capture;

public sealed class LinuxPipeWireCapture : IAudioCapture, IDisposable
{
    public int SampleRate => 48000;
    public int Channels => 2;
    public int BitsPerSample => 32;

    public bool IsCapturing => _isCapturing;

    public event EventHandler<byte[]>? DataAvailable;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<bool>? IsCapturingChanged;

    public string? TargetSink { get; set; }

    private volatile bool _isCapturing;
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private Task? _stderrTask;
    private int _restartCount;
    private const int MaxRestarts = 3;
    private const int ChunkSizeBytes = 48000 * 2 * sizeof(short) * 20 / 1000;

    public void Start()
    {
        if (_isCapturing) return;

        try
        {
            _restartCount = 0;
            _cts = new CancellationTokenSource();
            StartProcess();
        }
        catch (Exception ex)
        {
            _isCapturing = false;
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private void StartProcess()
    {
        var target = string.IsNullOrEmpty(TargetSink) ? GetDefaultMonitor() : $"{TargetSink}.monitor";
        var psi = new ProcessStartInfo
        {
            FileName = "pw-record",
            Arguments = $"--target={target} --format=s16 --rate=48000 --channels=2 --latency=20ms -",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            _process = Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            ErrorOccurred?.Invoke(this, new InvalidOperationException(
                "pw-record non trovato. Installa PipeWire: sudo pacman -S pipewire pipewire-pulse"));
            return;
        }

        if (_process == null)
        {
            ErrorOccurred?.Invoke(this, new InvalidOperationException("Impossibile avviare pw-record"));
            return;
        }

        _isCapturing = true;
        IsCapturingChanged?.Invoke(this, true);

        _readTask = Task.Run(() => ReadLoopAsync(_cts!.Token));
        _stderrTask = Task.Run(() => StderrLoopAsync(_cts!.Token));
    }

    private static string GetDefaultMonitor()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pactl",
                Arguments = "get-default-sink",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "@DEFAULT_MONITOR@";
            var name = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(2000);
            if (name.Length > 0)
                return $"{name}.monitor";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioBridge] pactl get-default-sink fallito: {ex.Message}");
        }
        return "@DEFAULT_MONITOR@";
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var stream = _process!.StandardOutput.BaseStream;
        var s16Buffer = new byte[ChunkSizeBytes];

        try
        {
            while (!ct.IsCancellationRequested && _isCapturing)
            {
                int totalRead = 0;
                while (totalRead < s16Buffer.Length && !ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(
                        s16Buffer.AsMemory(totalRead, s16Buffer.Length - totalRead), ct);
                    if (read == 0) break;
                    totalRead += read;
                }

                if (totalRead == 0) break;

                int sampleCount = totalRead / sizeof(short);
                var float32 = new byte[sampleCount * sizeof(float)];
                for (int i = 0; i < sampleCount; i++)
                {
                    short s = (short)(s16Buffer[i * 2] | (s16Buffer[i * 2 + 1] << 8));
                    float f = s / 32768f;
                    var fb = BitConverter.GetBytes(f);
                    float32[i * 4] = fb[0];
                    float32[i * 4 + 1] = fb[1];
                    float32[i * 4 + 2] = fb[2];
                    float32[i * 4 + 3] = fb[3];
                }

                DataAvailable?.Invoke(this, float32);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_isCapturing)
                ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            if (_isCapturing)
                HandleProcessExit();
        }
    }

    private async Task StderrLoopAsync(CancellationToken ct)
    {
        var reader = _process!.StandardError;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;
                Console.WriteLine($"[pw-record] {line}");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[pw-record] Errore lettura stderr: {ex.Message}");
        }
    }

    private void HandleProcessExit()
    {
        _isCapturing = false;
        IsCapturingChanged?.Invoke(this, false);

        _restartCount++;
        if (_restartCount <= MaxRestarts)
        {
            Console.WriteLine($"[AudioBridge] pw-record terminato, riavvio tentativo {_restartCount}/{MaxRestarts}...");
            Task.Delay(500).ContinueWith(_ =>
            {
                if (_cts?.IsCancellationRequested == false)
                {
                    try { StartProcess(); }
                    catch (Exception ex) { ErrorOccurred?.Invoke(this, ex); }
                }
            });
        }
        else
        {
            ErrorOccurred?.Invoke(this, new InvalidOperationException(
                "pw-record terminato inaspettatamente dopo 3 tentativi di riavvio"));
        }
    }

    public void Stop()
    {
        if (!_isCapturing) return;
        _isCapturing = false;
        _cts?.Cancel();

        try { _process?.Kill(); } catch { }
        try { _process?.WaitForExit(2000); } catch { }
        _process?.Dispose();
        _process = null;
        _cts?.Dispose();
        _cts = null;

        IsCapturingChanged?.Invoke(this, false);
    }

    public void Dispose() => Stop();
}
