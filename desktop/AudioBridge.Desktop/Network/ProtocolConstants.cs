using System;

namespace AudioBridge.Desktop.Network;

public static class ProtocolConstants
{
    public const ushort Magic = 0xCDAB;
    public const int HeaderSize = 18;

    public const int DefaultControlPort = 54321;
    public const int DefaultDataPort = 54322;
    public const string ServiceType = "_audiobridge._tcp.local";

    public const int MaxPayloadSize = 1200;
    public const int Mtu = 1280;

    public const int KeepAliveIntervalMs = 3000;
    public const int KeepAliveTimeoutMs = 200;
    public const int MaxMissedPongs = 3;
    public const int KeepAliveAbsoluteTimeoutMs = 10000;

    public const int DefaultSampleRate = 48000;
    public const int DefaultChannels = 2;
    public const int DefaultBitrate = 256000;
    public const int DefaultFrameSizeMs = 20;

    [Flags]
    public enum PacketFlags : byte
    {
        None = 0,
        Keyframe = 1 << 0,
        Silence = 1 << 1,
        ConfigChange = 1 << 2,
        PcmRaw = 1 << 3,
    }

    public const int RawFrameMs = 5;
}
