using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AudioBridge.Desktop.Network;

public sealed class TcpControlChannel : IDisposable
{
    private readonly int _controlPort;
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    private DateTime _lastPongReceived = DateTime.UtcNow;
    private bool _isConnected;

    public IPAddress? ClientIp { get; private set; }
    public string ClientName { get; private set; } = "";
    public bool IsConnected => _isConnected;

    public event EventHandler<NegotiatedParams>? HandshakeCompleted;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<double>? LatencyUpdated;

    public TcpControlChannel(int controlPort = ProtocolConstants.DefaultControlPort)
    {
        _controlPort = controlPort;
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _controlPort);
        try
        {
            _listener.Start();
        }
        catch (SocketException ex) when (ex.ErrorCode == 10048)
        {
            _listener = null;
            ErrorOccurred?.Invoke(this, $"Porta {_controlPort} già in uso — cambiala nelle impostazioni o chiudi l'altra istanza di AudioBridge");
            return;
        }
        Console.WriteLine($"[TcpControl] Listener avviato sulla porta {_controlPort}");
        _listenTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _client?.Close();
        _stream?.Close();
        _listener?.Stop();
        _isConnected = false;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        int cycle = 0;
        while (!ct.IsCancellationRequested)
        {
            cycle++;
            Console.WriteLine($"[TcpControl] AcceptLoopAsync — ciclo #{cycle}, attesa client...");
            try
            {
                _client = await _listener!.AcceptTcpClientAsync(ct);
                _client.NoDelay = true;
                _stream = _client.GetStream();
                ClientIp = ((IPEndPoint)_client.Client.RemoteEndPoint!).Address;
                _isConnected = true;

                Console.WriteLine($"[TcpControl] Client connesso da {ClientIp} (ciclo #{cycle})");
                await HandleClientAsync(_client, _stream, ct);
                Console.WriteLine($"[TcpControl] HandleClientAsync terminato per {ClientIp} — disconnessione client (ciclo #{cycle})");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[TcpControl] AcceptLoopAsync cancellato (ciclo #{cycle})");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpControl] AcceptLoopAsync ERRORE (ciclo #{cycle}): {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[TcpControl] _client={( _client?.Client?.RemoteEndPoint?.ToString() ?? "null" )}, _isConnected={_isConnected}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
            finally
            {
                Console.WriteLine($"[TcpControl] AcceptLoopAsync finally (ciclo #{cycle}) — _isConnected={_isConnected}");
                if (_isConnected)
                {
                    _isConnected = false;
                    var ip = ClientIp?.ToString() ?? "unknown";
                    ClientIp = null;
                    Console.WriteLine($"[TcpControl] Firing ClientDisconnected per {ip}");
                    ClientDisconnected?.Invoke(this, ip);
                }
                CleanupClient();
                Console.WriteLine($"[TcpControl] AcceptLoopAsync fine finally (ciclo #{cycle})");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, NetworkStream stream, CancellationToken ct)
    {
        // 1. Wait for HELLO from client
        var helloJson = await ReadLineAsync(stream, ct);
        if (helloJson == null)
        {
            Console.WriteLine("[TcpControl] Connessione chiusa dal client (HELLO mai ricevuto)");
            return;
        }

        Console.WriteLine($"[TcpControl] HELLO ricevuto: {helloJson}");

        // Validate HELLO
        JsonDocument helloDoc;
        try
        {
            helloDoc = JsonDocument.Parse(helloJson);
            var root = helloDoc.RootElement;
            if (root.TryGetProperty("type", out var t) && t.GetString() == "HELLO")
            {
                ClientName = root.TryGetProperty("client_name", out var cn) ? cn.GetString() ?? "Sconosciuto" : "Sconosciuto";
                Console.WriteLine($"[TcpControl] Client: {ClientName}");
            }
        }
        catch
        {
            ClientName = "Sconosciuto";
            Console.WriteLine("[TcpControl] HELLO malformato, continuo comunque");
        }

        // 2. Build negotiated params
        var sessionId = Guid.NewGuid().ToString();
        var negotiated = new NegotiatedParams(
            ClientIp!,
            ClientName,
            ProtocolConstants.DefaultSampleRate,
            ProtocolConstants.DefaultChannels,
            ProtocolConstants.DefaultBitrate,
            ProtocolConstants.DefaultFrameSizeMs,
            ProtocolConstants.DefaultDataPort,
            sessionId
        );

        // 3. Send WELCOME
        var welcome = new
        {
            type = "WELCOME",
            version = 1,
            server_name = Environment.MachineName,
            server_id = $"desktop-{Environment.MachineName}",
            session_id = sessionId,
            negotiated = new
            {
                sample_rate = negotiated.SampleRate,
                channels = negotiated.Channels,
                bitrate = negotiated.Bitrate,
                frame_size_ms = negotiated.FrameSizeMs,
                udp_port = negotiated.UdpPort
            }
        };
        await WriteLineAsync(stream, JsonSerializer.Serialize(welcome), ct);
        Console.WriteLine($"[TcpControl] WELCOME inviato (session={sessionId})");

        // 4. Fire HandshakeCompleted so MainViewModel can start UDP stream
        HandshakeCompleted?.Invoke(this, negotiated);

        // 5. Send STREAM_START notification
        await WriteLineAsync(stream, """{"type":"STREAM_START"}""", ct);

        // 6. Keep-alive loop
        _lastPongReceived = DateTime.UtcNow;
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pingTask = PingLoopAsync(stream, pingCts.Token);
        var readTask = ReadLoopAsync(stream, pingCts.Token);

        await Task.WhenAny(pingTask, readTask);
        pingCts.Cancel();
    }

    private async Task PingLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(ProtocolConstants.KeepAliveIntervalMs, ct);

            if ((DateTime.UtcNow - _lastPongReceived).TotalMilliseconds >= ProtocolConstants.KeepAliveAbsoluteTimeoutMs)
            {
                Console.WriteLine("[TcpControl] Timeout keep-alive: nessun PONG per 10s");
                break;
            }

            var ping = new { type = "PING", ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            try
            {
                await WriteLineAsync(stream, JsonSerializer.Serialize(ping), ct);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task ReadLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await ReadLineAsync(stream, ct);
            if (line == null) break;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "PONG":
                        _lastPongReceived = DateTime.UtcNow;
                        if (root.TryGetProperty("ts", out var tsPong))
                        {
                            var rtt = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - tsPong.GetInt64());
                            if (rtt >= 0)
                                LatencyUpdated?.Invoke(this, rtt);
                        }
                        break;

                    case "PING":
                        var ts = root.GetProperty("ts").GetInt64();
                        var pong = new { type = "PONG", ts, rtt_ms = 1.0 };
                        await WriteLineAsync(stream, JsonSerializer.Serialize(pong), ct);
                        break;

                    case "PAUSE":
                        Console.WriteLine("[TcpControl] PAUSE dal client");
                        break;

                    case "RESUME":
                        Console.WriteLine("[TcpControl] RESUME dal client");
                        break;

                    case "SET_BITRATE":
                        if (root.TryGetProperty("bps", out var bps))
                            Console.WriteLine($"[TcpControl] SET_BITRATE: {bps.GetInt32()}");
                        break;

                    case "SET_FRAME_SIZE":
                        if (root.TryGetProperty("ms", out var ms))
                            Console.WriteLine($"[TcpControl] SET_FRAME_SIZE: {ms.GetInt32()}");
                        break;

                    case "GET_STATS":
                        await WriteLineAsync(stream, """{"type":"STATS","packets_sent":0,"bytes_sent":0,"packets_lost_estimated":0,"current_bitrate_bps":0,"avg_rtt_ms":0}""", ct);
                        break;

                    case "HELLO":
                        // ignore, already handled
                        break;

                    default:
                        Console.WriteLine($"[TcpControl] Messaggio sconosciuto: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpControl] Errore parsing messaggio: {ex.Message}");
            }
        }
    }

    private static async Task<string?> ReadLineAsync(NetworkStream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buf = new byte[1];
        while (!ct.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buf, 0, 1, ct);
            if (read == 0) return null;
            if (buf[0] == '\n')
                return Encoding.UTF8.GetString(ms.ToArray());
            if (ms.Length > 65536) return null;
            ms.WriteByte(buf[0]);
        }
        return null;
    }

    private static async Task WriteLineAsync(NetworkStream stream, string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(bytes, 0, bytes.Length, ct);
        await stream.FlushAsync(ct);
    }

    private void CleanupClient()
    {
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
    }

    public void Dispose() => Stop();
}

public readonly record struct NegotiatedParams(
    IPAddress ClientIp,
    string ClientName,
    int SampleRate,
    int Channels,
    int Bitrate,
    int FrameSizeMs,
    int UdpPort,
    string SessionId
);
