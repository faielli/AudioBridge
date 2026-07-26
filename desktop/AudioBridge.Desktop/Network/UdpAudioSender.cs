using System;
using System.Net;
using System.Net.Sockets;

namespace AudioBridge.Desktop.Network;

public sealed class UdpAudioSender : IDisposable
{
    private readonly UdpClient _udp;
    private uint _sequence;
    private int _frameCount;

    public int LocalPort { get; }
    public IPEndPoint? RemoteEndPoint { get; private set; }
    public bool IsActive { get; private set; }

    public UdpAudioSender()
    {
        _udp = new UdpClient(0);
        LocalPort = ((IPEndPoint?)_udp.Client.LocalEndPoint)?.Port ?? 0;
        Console.WriteLine($"[DEBUG] UdpAudioSender creato — LocalPort={LocalPort}");
    }

    public void SetRemote(IPEndPoint remote)
    {
        Console.WriteLine($"[DEBUG] UdpAudioSender.SetRemote — vecchio={RemoteEndPoint} nuovo={remote}");
        RemoteEndPoint = remote;
    }

    public int Send(byte[] packet, int length)
    {
        if (RemoteEndPoint == null)
        {
            Console.WriteLine($"[DEBUG] UdpAudioSender.Send — RemoteEndPoint null, drop {length} bytes");
            return 0;
        }

        var sent = _udp.Send(packet, length, RemoteEndPoint);
        return sent;
    }

    public void SendAudioFrame(ReadOnlySpan<byte> opusData, ulong timestampNtp, ProtocolConstants.PacketFlags flags)
    {
        if (RemoteEndPoint == null)
        {
            Console.WriteLine($"[DEBUG] UdpAudioSender.SendAudioFrame — RemoteEndPoint null, drop seq={_sequence}");
            return;
        }

        _frameCount++;
        if (_frameCount <= 5 || _frameCount % 50 == 0)
            Console.WriteLine($"[DEBUG] UdpAudioSender.SendAudioFrame #{_frameCount} — seq={_sequence} len={opusData.Length} dest={RemoteEndPoint}");

        var payloadLen = Math.Min(opusData.Length, ProtocolConstants.MaxPayloadSize);
        var totalSize = ProtocolConstants.HeaderSize + payloadLen;
        var packet = new byte[totalSize];

        var header = new PacketHeader(
            sequence: _sequence++,
            timestampNtp: timestampNtp,
            flags: flags,
            payloadLen: (ushort)payloadLen
        );
        header.Write(packet);

        opusData[..payloadLen].CopyTo(packet.AsSpan(ProtocolConstants.HeaderSize));

        _udp.Send(packet, totalSize, RemoteEndPoint);
    }

    public void Dispose()
    {
        _udp.Dispose();
    }
}
