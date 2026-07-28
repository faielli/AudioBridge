using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AudioBridge.Desktop.Capture;
using AudioBridge.Desktop.Controls;
using AudioBridge.Desktop.Network;
using AudioBridge.Desktop.Services;

namespace AudioBridge.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IAudioCapture _capture;
    private readonly SettingsService _settingsService;
    private readonly Models.AppSettings _appSettings;

    public ObservableCollection<string> LogEntries { get; } = [];

    private void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogEntries.Add(entry);
        if (LogEntries.Count > 20)
            LogEntries.RemoveAt(0);
        Console.WriteLine(entry);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StreamButtonText))]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StreamButtonText))]
    private bool _isStreaming;

    [ObservableProperty]
    private bool _isLightTheme;

    [ObservableProperty]
    private string _statusText = "Disconnesso";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private bool _isCapturing;

    public string CaptureButtonText => IsCapturing ? "Arresta cattura" : "Avvia cattura";

    [ObservableProperty]
    private double _peakLevel;

    [ObservableProperty]
    private string _captureInfo = "Inattivo";

    [ObservableProperty]
    private string _clientName = "";

    private static readonly (int Bitrate, int FrameSizeMs, string BufferNote)[] PresetSettings = [
        (256000, 20, "Medio"),
        (192000, 15, "Medio-basso"),
        (128000, 10, "Minimo")
    ];

    private int _currentBitrate = ProtocolConstants.DefaultBitrate;
    private int _currentFrameSizeMs = ProtocolConstants.DefaultFrameSizeMs;

    [ObservableProperty]
    private int _selectedProfile;

    public string[] Profiles => ["Musica", "Film", "Gaming"];

    partial void OnSelectedProfileChanged(int value)
    {
        if (value < 0 || value >= PresetSettings.Length) return;
        var (bitrate, frameSizeMs, _) = PresetSettings[value];
        _currentBitrate = bitrate;
        _currentFrameSizeMs = frameSizeMs;

        if (_session is { IsStreaming: true })
        {
            _session.SetBitrate(bitrate);
            _session.SetFrameSizeMs(frameSizeMs);
            AddLog($"Preset applicato: {Profiles[value]} — {bitrate / 1000}kbps, {frameSizeMs}ms");
        }

        StreamInfo = $"Preset: {Profiles[value]} — {bitrate / 1000} kbps, {frameSizeMs} ms";
        StreamInfoSecondary = "";

        _appSettings.SelectedProfile = value;
        _settingsService.Save(_appSettings);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LatencyBrush))]
    [NotifyPropertyChangedFor(nameof(LatencyDisplay))]
    private double _latencyMs;

    public string LatencyDisplay => LatencyMs > 0 ? $"{LatencyMs:F0} ms" : "—";

    private static readonly SolidColorBrush LatencyGreen = new(Color.FromArgb(255, 126, 217, 195));
    private static readonly SolidColorBrush LatencyYellow = new(Color.FromArgb(255, 242, 184, 75));
    private static readonly SolidColorBrush LatencyRed = new(Color.FromArgb(255, 255, 107, 107));
    private static readonly SolidColorBrush LatencyDisabled = new(Color.FromArgb(255, 74, 78, 90));

    public IBrush LatencyBrush
    {
        get
        {
            if (LatencyMs <= 0) return LatencyDisabled;
            if (LatencyMs < 50) return LatencyGreen;
            if (LatencyMs < 100) return LatencyYellow;
            return LatencyRed;
        }
    }

    public MainViewModel(IAudioCapture capture, SettingsService? settingsService = null)
    {
        _capture = capture;
        _settingsService = settingsService ?? new SettingsService();
        _capture.DataAvailable += OnDataAvailable;
        _capture.IsCapturingChanged += OnIsCapturingChanged;
        _capture.ErrorOccurred += OnErrorOccurred;

        _appSettings = _settingsService.Load();
        _currentBitrate = _appSettings.Bitrate;
        _currentFrameSizeMs = _appSettings.FrameSizeMs;
        _selectedProfile = _appSettings.SelectedProfile;
        TargetIp = string.IsNullOrWhiteSpace(_appSettings.ManualIp) ? GetLocalIpAddress() : _appSettings.ManualIp;

        if (_appSettings.MdnsEnabled)
        {
            _mdns.Start(Environment.MachineName, ProtocolConstants.DefaultControlPort);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_settingsService);
        var window = new Views.SettingsWindow { DataContext = vm };
        window.Show();
    }

    private bool _isRecordingTest;

    [RelayCommand]
    private void ToggleCapture()
    {
        if (_capture.IsCapturing)
        {
            _capture.Stop();
        }
        else
        {
            _capture.Start();
        }
    }

    private EventHandler<byte[]>? _testHandler;

    [RelayCommand]
    private async Task RecordTest()
    {
        if (_isRecordingTest)
        {
            Console.WriteLine("[AudioBridge] Test già in corso, ignorato.");
            return;
        }

        _isRecordingTest = true;
        var wasCapturing = _capture.IsCapturing;

        try
        {
            var filePath = "/tmp/audiobridge_test.wav";

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms))
            {
                int sampleRate = _capture.SampleRate;
                short channels = (short)_capture.Channels;
                short bitsPerSample = 16;
                int dataSize = 0;

                writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                writer.Write(0);
                writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bitsPerSample / 8);
                writer.Write((short)(channels * bitsPerSample / 8));
                writer.Write(bitsPerSample);
                writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                writer.Write(dataSize);

                _testHandler = (s, data) =>
                {
                    var sampleCount = data.Length / 4;
                    var pcm = new byte[sampleCount * 2];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        var sample = BitConverter.ToSingle(data, i * 4);
                        var clamped = Math.Clamp(sample, -1f, 1f);
                        var pcm16 = (short)(clamped * 32767f);
                        pcm[i * 2] = (byte)(pcm16 & 0xFF);
                        pcm[i * 2 + 1] = (byte)((pcm16 >> 8) & 0xFF);
                    }
                    writer.BaseStream.Write(pcm, 0, pcm.Length);
                };

                _capture.DataAvailable += _testHandler;

                if (!wasCapturing)
                    _capture.Start();

                Console.WriteLine($"[AudioBridge] Registrazione avviata -> {filePath}");
                CaptureInfo = "Registrazione 5s in corso...";

                await Task.Delay(5000);

                Console.WriteLine("[AudioBridge] 5s trascorsi, rimuovo handler e fermo cattura.");

                writer.BaseStream.Seek(4, SeekOrigin.Begin);
                writer.Write((int)(writer.BaseStream.Length - 8));
                writer.BaseStream.Seek(40, SeekOrigin.Begin);
                writer.Write((int)(writer.BaseStream.Length - 44));
            }

            await File.WriteAllBytesAsync(filePath, ms.ToArray());
            CaptureInfo = $"Test salvato: {filePath}";

            _capture.DataAvailable -= _testHandler;
            _capture.Stop();
            _testHandler = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioBridge] Errore test: {ex.Message}");
            CaptureInfo = $"Errore test: {ex.Message}";
        }
        finally
        {
            if (_testHandler != null)
            {
                _capture.DataAvailable -= _testHandler;
                _testHandler = null;
            }
            _isRecordingTest = false;
            if (wasCapturing)
                _capture.Start();
        }
    }

    private StreamSession? _session;
    private readonly MdnsPublisher _mdns = new();
    private TcpControlChannel? _controlChannel;

    [ObservableProperty]
    private string _targetIp = GetLocalIpAddress();

    [ObservableProperty]
    private string _streamInfo = "Trasmissione ferma";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStreamInfoSecondary))]
    private string _streamInfoSecondary = "";

    public bool HasStreamInfoSecondary => !string.IsNullOrEmpty(StreamInfoSecondary);

    public string StreamButtonText => ConnectionState switch
    {
        Controls.ConnectionState.Connecting => "In attesa connessione…",
        Controls.ConnectionState.Connected => "Interrompi trasmissione",
        _ => "Avvia trasmissione"
    };

    [RelayCommand(CanExecute = nameof(CanToggleStreaming))]
    private void ToggleStreaming()
    {
        if (IsStreaming)
        {
            StopStreaming();
        }
        else
        {
            StartStreaming();
        }
    }

    private bool CanToggleStreaming()
    {
        return ConnectionState != Controls.ConnectionState.Connecting;
    }

    private void StartStreaming()
    {
        if (_controlChannel != null) return;

        if (!IPAddress.TryParse(TargetIp, out _))
        {
            StreamInfo = "IP non valido";
            StreamInfoSecondary = "";
            return;
        }

        try
        {
            ConnectionState = Controls.ConnectionState.Connecting;

            _controlChannel = new TcpControlChannel();
            _controlChannel.HandshakeCompleted += OnHandshakeCompleted;
            _controlChannel.ClientDisconnected += OnTcpDisconnected;
            _controlChannel.ErrorOccurred += OnTcpError;
            _controlChannel.LatencyUpdated += OnLatencyUpdated;
            _controlChannel.Start();

            IsStreaming = true;
            StatusText = "In attesa";
            StreamInfo = $"In attesa connessione sulla porta {ProtocolConstants.DefaultControlPort} ({TargetIp})...";
            StreamInfoSecondary = "";

            AddLog($"Avvio connessione verso {TargetIp}:{ProtocolConstants.DefaultControlPort}");
        }
        catch (Exception ex)
        {
            _controlChannel?.Dispose();
            _controlChannel = null;
            ConnectionState = Controls.ConnectionState.Disconnected;
            IsStreaming = false;
            StatusText = "Disconnesso";
            StreamInfo = $"Errore avvio controllo: {ex.Message}";
            StreamInfoSecondary = "";
            AddLog($"Errore avvio: {ex.Message}");
        }
    }

    private void StopStreaming()
    {
        StopUdpStream();
        _controlChannel?.Dispose();
        _controlChannel = null;
        IsStreaming = false;
        if (!_isRecordingTest)
        {
            _capture.Stop();
            IsCapturing = false;
        }
        ConnectionState = Controls.ConnectionState.Disconnected;
        StatusText = "Disconnesso";
        ClientName = "";
        LatencyMs = 0;
        StreamInfo = "Trasmissione ferma";
        StreamInfoSecondary = "";
        AddLog("Trasmissione terminata");
    }

    private void StopUdpStream()
    {
        if (_session == null) return;
        _session.IsStreamingChanged -= OnSessionStreamingChanged;
        _session.Dispose();
        _session = null;
    }

    private void OnHandshakeCompleted(object? sender, NegotiatedParams p)
    {
        Console.WriteLine($"[DEBUG] OnHandshakeCompleted chiamato — client={p.ClientName} ip={p.ClientIp} udpPort={p.UdpPort} sessionId={p.SessionId}");
        Dispatcher.UIThread.Post(() =>
        {
            Console.WriteLine($"[DEBUG] OnHandshakeCompleted UI thread — inizio");
            StopUdpStream();
            ClientName = p.ClientName;
            StatusText = "Connesso";
            ConnectionState = Controls.ConnectionState.Connected;

            try
            {
                Console.WriteLine($"[DEBUG] Creazione StreamSession verso {p.ClientIp}:{p.UdpPort}");
                _session = new StreamSession(_capture, new IPEndPoint(p.ClientIp, p.UdpPort), useRawPcm: false, bitrate: _currentBitrate, frameSizeMs: _currentFrameSizeMs);
                _session.IsStreamingChanged += OnSessionStreamingChanged;
                Console.WriteLine($"[DEBUG] Chiamata _session.Start()...");
                _session.Start();
                Console.WriteLine($"[DEBUG] _session.Start() completato — IsStreaming={_session.IsStreaming}");

                StreamInfo = $"Streaming verso {p.ClientName} ({p.ClientIp}:{p.UdpPort})";
                StreamInfoSecondary = $"{Profiles[SelectedProfile]} — {_currentBitrate / 1000} kbps, {_currentFrameSizeMs} ms";
                AddLog($"Connesso a {p.ClientName} ({p.ClientIp}:{p.UdpPort})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ERRORE in OnHandshakeCompleted: {ex.GetType().Name}: {ex.Message}");
                _capture.Stop();
                IsCapturing = false;
                IsStreaming = false;
                _controlChannel?.Dispose();
                _controlChannel = null;
                ConnectionState = Controls.ConnectionState.Disconnected;
                StatusText = "Errore connessione";
                StreamInfo = $"Errore stream UDP: {ex.Message}";
                StreamInfoSecondary = "";
                AddLog($"Errore stream UDP: {ex.Message}");
            }
            Console.WriteLine($"[DEBUG] OnHandshakeCompleted UI thread — fine (IsStreaming={IsStreaming} IsCapturing={IsCapturing})");
        });
    }

    private void OnTcpDisconnected(object? sender, string clientIp)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Console.WriteLine($"[DEBUG] OnTcpDisconnected — clientIp={clientIp} IsStreaming={IsStreaming} IsCapturing={IsCapturing} _session={_session?.GetHashCode()}");

            StopUdpStream();
            _capture.Stop();
            IsCapturing = false;
            ConnectionState = Controls.ConnectionState.Disconnected;
            StatusText = "Disconnesso";
            LatencyMs = 0;
            if (IsStreaming)
            {
                StreamInfo = "Client disconnesso — in attesa riconnessione...";
                StreamInfoSecondary = "";
            }
            AddLog($"Client disconnesso ({clientIp})");

            Console.WriteLine($"[DEBUG] OnTcpDisconnected fine — IsStreaming={IsStreaming} IsCapturing={IsCapturing} _session={_session?.GetHashCode()}");
        });
    }

    private void OnTcpError(object? sender, string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsStreaming) return;
            AddLog($"Errore TCP: {msg}");
            StopStreaming();
        });
    }

    private void OnLatencyUpdated(object? sender, double rttMs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LatencyMs = rttMs;
        });
    }

    private void OnSessionStreamingChanged(object? sender, bool streaming)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!streaming)
            {
                StopUdpStream();
                StreamInfo = "Streaming fermo";
                StreamInfoSecondary = "";
            }
        });
    }

    private void OnDataAvailable(object? sender, byte[] data)
    {
        var floatBuffer = new float[data.Length / 4];
        Buffer.BlockCopy(data, 0, floatBuffer, 0, data.Length);

        double sumSq = 0;
        for (int i = 0; i < floatBuffer.Length; i++)
            sumSq += floatBuffer[i] * floatBuffer[i];
        var rms = Math.Sqrt(sumSq / floatBuffer.Length);
        var level = Math.Clamp(rms * 4.0, 0.0, 1.0);

        Dispatcher.UIThread.Post(() => PeakLevel = level);
    }

    private void OnIsCapturingChanged(object? sender, bool capturing)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsCapturing = capturing;
            CaptureInfo = capturing
                ? $"Cattura in corso — {_capture.SampleRate} Hz, {_capture.Channels} canali, {_capture.BitsPerSample} bit"
                : "Inattivo";
        });
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var msg = "Dispositivo audio disconnesso o cambiato — riseleziona la sorgente nelle impostazioni";
            AddLog($"Errore cattura audio: {ex.Message}");

            if (!IsStreaming)
            {
                _capture.Stop();
                return;
            }

            StopUdpStream();
            _controlChannel?.Dispose();
            _controlChannel = null;
            IsStreaming = false;
            _capture.Stop();
            IsCapturing = false;
            ConnectionState = Controls.ConnectionState.Disconnected;
            StatusText = "Errore dispositivo audio";
            ClientName = "";
            LatencyMs = 0;
            StreamInfo = msg;
            StreamInfoSecondary = "";
        });
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            var entry = Dns.GetHostEntry(Dns.GetHostName());
            var rfc1918 = (System.Net.IPAddress?)null;
            foreach (var addr in entry.AddressList)
            {
                if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    continue;
                var bytes = addr.GetAddressBytes();
                if (bytes[0] == 10 || bytes[0] == 192 ||
                    (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31))
                    return addr.ToString();
                rfc1918 ??= addr;
            }
            if (rfc1918 != null) return rfc1918.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
}