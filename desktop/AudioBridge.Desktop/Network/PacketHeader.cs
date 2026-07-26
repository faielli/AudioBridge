using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AudioBridge.Desktop.Network;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct PacketHeader
{
    public readonly ushort Magic;
    public readonly uint Sequence;
    public readonly ulong TimestampNtp;
    public readonly byte Flags;
    public readonly byte Reserved;
    public readonly ushort PayloadLen;

    public PacketHeader(uint sequence, ulong timestampNtp, ProtocolConstants.PacketFlags flags, ushort payloadLen)
    {
        Magic = ProtocolConstants.Magic;
        Sequence = sequence;
        TimestampNtp = timestampNtp;
        Flags = (byte)flags;
        Reserved = 0;
        PayloadLen = payloadLen;
    }

    public int TotalSize => ProtocolConstants.HeaderSize + PayloadLen;

    public int Write(Span<byte> dest)
    {
        Unsafe.As<byte, PacketHeader>(ref dest[0]) = this;
        return ProtocolConstants.HeaderSize;
    }

    public static PacketHeader Read(ReadOnlySpan<byte> src)
    {
        return Unsafe.As<byte, PacketHeader>(ref MemoryMarshal.GetReference(src));
    }

    public static bool TryRead(ReadOnlySpan<byte> src, out PacketHeader header)
    {
        if (src.Length < ProtocolConstants.HeaderSize)
        {
            header = default;
            return false;
        }
        header = Read(src);
        return header.Magic == ProtocolConstants.Magic;
    }
}
