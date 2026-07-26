using System.Net;
using System.Net.Sockets;

var port = args.Length > 0 ? int.Parse(args[0]) : 54322;
var saveDir = args.Length > 1 ? args[1] : null;

using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
Console.WriteLine($"[TestReceiver] In ascolto su UDP 0.0.0.0:{port}");
Console.WriteLine("[TestReceiver] Premi Ctrl+C per fermare.");

uint totalPackets = 0;
long totalBytes = 0;
var lastLog = DateTime.UtcNow;
var opusStream = saveDir != null
    ? new FileStream(Path.Combine(saveDir, $"audiobridge-received_{DateTime.Now:yyyyMMdd-HHmmss}.opus"), FileMode.Create)
    : null;

try
{
    while (true)
    {
        var result = await udp.ReceiveAsync();
        var data = result.Buffer;
        totalPackets++;
        totalBytes += data.Length;

        if (data.Length < 18)
        {
            Console.WriteLine($"Pacchetto troppo corto: {data.Length} byte");
            continue;
        }

        var magic = (ushort)(data[0] | (data[1] << 8));
        if (magic != 0xCDAB)
        {
            Console.WriteLine($"Magic errato: 0x{magic:X4}");
            continue;
        }

        var seq = (uint)(data[2] | (data[3] << 8) | (data[4] << 16) | (data[5] << 24));
        var flags = data[14];
        var payloadLen = (ushort)(data[16] | (data[17] << 8));

        if (data.Length < 18 + payloadLen)
        {
            Console.WriteLine($"Payload dichiarato {payloadLen} ma pacchetto solo {data.Length} byte");
            continue;
        }

        if (opusStream != null)
        {
            var lenBytes = BitConverter.GetBytes((ushort)payloadLen);
            opusStream.Write(lenBytes, 0, 2);
            opusStream.Write(data, 18, payloadLen);
        }

        var now = DateTime.UtcNow;
        if ((now - lastLog).TotalSeconds >= 5)
        {
            var rate = totalPackets / (now - lastLog).TotalSeconds;
            Console.WriteLine(
                $"[{now:HH:mm:ss}] Pacchetti: {totalPackets} | Bytes: {totalBytes / 1024.0:F1} KB | " +
                $"Seq: {seq} | Payload: {payloadLen}B | Flags: 0x{flags:X2} | {rate:F0} pkt/s"
            );
            lastLog = now;
        }

        if (totalPackets == 1)
            Console.WriteLine($"Primo pacchetto ricevuto! Seq={seq}, Flags=0x{flags:X2}, PayloadLen={payloadLen}");
    }
}
catch (OperationCanceledException) { }
catch (Exception ex)
{
    Console.WriteLine($"Errore: {ex.Message}");
}
finally
{
    opusStream?.Dispose();
    Console.WriteLine($"[TestReceiver] Totale: {totalPackets} pacchetti, {totalBytes} byte ({totalBytes / 1024.0 / 1024.0:F2} MB)");
}
