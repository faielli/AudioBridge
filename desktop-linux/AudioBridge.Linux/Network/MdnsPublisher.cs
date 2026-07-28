using Makaretu.Dns;
using System;

namespace AudioBridge.Desktop.Network;

public sealed class MdnsPublisher : IDisposable
{
    private ServiceDiscovery? _sd;
    private ServiceProfile? _profile;

    public void Start(string serviceName, int port)
    {
        Stop();

        _profile = new ServiceProfile(serviceName, ProtocolConstants.ServiceType, (ushort)port);
        _sd = new ServiceDiscovery();

        if (_sd.Probe(_profile))
        {
            Console.WriteLine($"[AudioBridge] mDNS: name conflict on '{serviceName}', advertising anyway");
        }

        _sd.Advertise(_profile);
        _sd.Announce(_profile);

        Console.WriteLine($"[AudioBridge] mDNS: publishing {serviceName}.{ProtocolConstants.ServiceType} port {port}");
    }

    public void Stop()
    {
        if (_sd != null)
        {
            try { _sd.Unadvertise(); } catch { }
            _sd.Dispose();
            _sd = null;
        }
        _profile = null;
    }

    public void Dispose() => Stop();
}
