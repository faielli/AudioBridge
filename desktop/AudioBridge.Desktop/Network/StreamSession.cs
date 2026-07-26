using AudioBridge.Desktop.Capture;
using System;
using System.Net;
using System.Threading;

namespace AudioBridge.Desktop.Network;

public sealed class StreamSession : IDisposable
{
    private readonly IAudioCapture _capture;
    private readonly OpusEncoderWrapper _encoder;
    private readonly UdpAudioSender _sender;
    private readonly CancellationTokenSource _cts = new();

    private readonly short[] _pcmBuffer = new short[96000];
    private int _pcmBufferPos;
    private uint _totalPackets;
    private long _totalBytes;
    private bool _isStreaming;

    public event EventHandler<bool>? IsStreamingChanged;
#pragma warning disable CS0067
    public event EventHandler<StreamStats>? StatsUpdated;
#pragma warning restore CS0067

    public bool IsStreaming => _isStreaming;
    public int SampleRate => _encoder.SampleRate;
    public int Channels => _encoder.Channels;
    public int Bitrate => _encoder.Bitrate;
    public int FrameSizeMs => _encoder.FrameSizeMs;

    private readonly bool _useRawPcm;

    public StreamSession(IAudioCapture capture, IPEndPoint remoteEndPoint, bool useRawPcm = false, int? bitrate = null, int? frameSizeMs = null)
    {
        _capture = capture;
        _useRawPcm = useRawPcm;
        _encoder = new OpusEncoderWrapper(
            ProtocolConstants.DefaultSampleRate,
            ProtocolConstants.DefaultChannels,
            bitrate ?? ProtocolConstants.DefaultBitrate,
            frameSizeMs ?? ProtocolConstants.DefaultFrameSizeMs
        );
        _sender = new UdpAudioSender();
        _sender.SetRemote(remoteEndPoint);
    }

    public void Start()
    {
        Console.WriteLine($"[DEBUG] StreamSession.Start() — _isStreaming={_isStreaming} sender={_sender.RemoteEndPoint}");
        if (_isStreaming)
        {
            Console.WriteLine($"[DEBUG] StreamSession.Start() — già in streaming, skip");
            return;
        }
        _isStreaming = true;
        IsStreamingChanged?.Invoke(this, true);

        Console.WriteLine($"[DEBUG] StreamSession.Start() — sottoscrizione DataAvailable e chiamata _capture.Start()");
        _capture.DataAvailable += OnDataAvailable;
        _capture.Start();

        Console.WriteLine($"[DEBUG] StreamSession.Start() — completato, sender={_sender.RemoteEndPoint}");
    }

    public void Stop()
    {
        Console.WriteLine($"[DEBUG] StreamSession.Stop() — _isStreaming={_isStreaming} pacchetti={_totalPackets}");
        if (!_isStreaming)
        {
            Console.WriteLine($"[DEBUG] StreamSession.Stop() — non in streaming, skip");
            return;
        }
        _isStreaming = false;
        IsStreamingChanged?.Invoke(this, false);

        Console.WriteLine($"[DEBUG] StreamSession.Stop() — rimozione handler DataAvailable e _capture.Stop()");
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Stop();

        Console.WriteLine($"[DEBUG] StreamSession.Stop() — fermato. Pacchetti: {_totalPackets}, Bytes: {_totalBytes}");
    }

    private int _dataEventCount;
    private void OnDataAvailable(object? sender, byte[] data)
    {
        _dataEventCount++;
        if (_dataEventCount <= 5 || _dataEventCount % 100 == 0)
            Console.WriteLine($"[DEBUG] StreamSession.OnDataAvailable #{_dataEventCount} — _isStreaming={_isStreaming} dataLen={data.Length} cancelled={_cts.IsCancellationRequested}");

        if (!_isStreaming || _cts.IsCancellationRequested)
            return;

        var sampleCount = data.Length / 4;

        for (int i = 0; i < sampleCount; i++)
        {
            if (_pcmBufferPos >= _pcmBuffer.Length)
                break;

            var sample = BitConverter.ToSingle(data, i * 4);
            _pcmBuffer[_pcmBufferPos++] = (short)(Math.Clamp(sample, -1f, 1f) * 32767f);
        }

        if (_useRawPcm)
            FlushRawFrames();
        else
            FlushOpusFrames();
    }

    private void FlushRawFrames()
    {
        var blockShorts = ProtocolConstants.DefaultSampleRate * ProtocolConstants.DefaultChannels
            * ProtocolConstants.DefaultFrameSizeMs / 1000;
        var subShorts = ProtocolConstants.DefaultSampleRate * ProtocolConstants.DefaultChannels
            * ProtocolConstants.RawFrameMs / 1000;
        var subCount = ProtocolConstants.DefaultFrameSizeMs / ProtocolConstants.RawFrameMs;

        while (_pcmBufferPos >= blockShorts)
        {
            for (int sub = 0; sub < subCount; sub++)
            {
                var offset = sub * subShorts;
                var rawPayload = new byte[subShorts * 2];
                Buffer.BlockCopy(_pcmBuffer, offset * sizeof(short), rawPayload, 0, subShorts * sizeof(short));

                var ntp = (ulong)(DateTime.UtcNow - new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                var flags = ProtocolConstants.PacketFlags.PcmRaw;
                if (_totalPackets == 0 && sub == 0)
                    flags |= ProtocolConstants.PacketFlags.Keyframe;

                _sender.SendAudioFrame(rawPayload.AsSpan(), ntp, flags);
                _totalPackets++;
                _totalBytes += ProtocolConstants.HeaderSize + rawPayload.Length;
            }

            var remaining = _pcmBufferPos - blockShorts;
            if (remaining > 0)
                Array.Copy(_pcmBuffer, blockShorts, _pcmBuffer, 0, remaining);
            _pcmBufferPos = remaining;
        }
    }

    private void FlushOpusFrames()
    {
        while (_pcmBufferPos >= _encoder.FrameSamples * _encoder.Channels)
        {
            var opusBuf = new byte[ProtocolConstants.MaxPayloadSize];
            var encoded = _encoder.Encode(_pcmBuffer, 0, _encoder.FrameSamples, opusBuf, 0, opusBuf.Length);

            if (encoded > 0)
            {
                var ntp = (ulong)(DateTime.UtcNow - new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                var flags = ProtocolConstants.PacketFlags.None;
                if (_totalPackets == 0)
                    flags |= ProtocolConstants.PacketFlags.Keyframe;

                _sender.SendAudioFrame(opusBuf.AsSpan(0, encoded), ntp, flags);
                _totalPackets++;
                _totalBytes += ProtocolConstants.HeaderSize + encoded;
            }

            var frameShorts = _encoder.FrameSamples * _encoder.Channels;
            var remaining = _pcmBufferPos - frameShorts;
            if (remaining > 0)
                Array.Copy(_pcmBuffer, frameShorts, _pcmBuffer, 0, remaining);
            _pcmBufferPos = remaining;
        }
    }

    public void SetBitrate(int bitrate) => _encoder.SetBitrate(bitrate);

    public void SetFrameSizeMs(int frameSizeMs) => _encoder.SetFrameSizeMs(frameSizeMs);

    public void Dispose()
    {
        _cts.Cancel();
        Stop();
        _encoder.Dispose();
        _sender.Dispose();
        _cts.Dispose();
    }
}

public readonly record struct StreamStats(
    uint PacketsSent,
    long BytesSent,
    int CurrentBitrate,
    double AvgRttMs
);
