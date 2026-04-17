using System.Net.Sockets;
using System.Text;
using Hl7Bridge.Service.Configuration;
using Microsoft.Extensions.Options;

namespace Hl7Bridge.Service.Infrastructure;

public sealed class MllpClient(IOptions<BridgeOptions> options, ILogger<MllpClient> logger) : IMllpClient
{
    private const byte Sb = 0x0B;
    private const byte Eb = 0x1C;
    private const byte Cr = 0x0D;

    private readonly BridgeOptions _options = options.Value;
    private readonly ILogger<MllpClient> _logger = logger;

    public async Task<string> SendAndReceiveAckAsync(string hl7Payload, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_options.Lis.Host, _options.Lis.Port, cancellationToken);
        client.ReceiveTimeout = _options.Lis.AckTimeoutMs;
        client.SendTimeout = _options.Lis.AckTimeoutMs;

        await using var stream = client.GetStream();
        var body = Encoding.UTF8.GetBytes(hl7Payload);

        var framed = new byte[body.Length + 3];
        framed[0] = Sb;
        Buffer.BlockCopy(body, 0, framed, 1, body.Length);
        framed[^2] = Eb;
        framed[^1] = Cr;

        await stream.WriteAsync(framed, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        _logger.LogInformation("Sent HL7 message with payload length {Length} bytes.", body.Length);

        var ack = await ReadMllpFrameAsync(stream, _options.Lis.AckTimeoutMs, cancellationToken);

        if (!ack.Contains("MSA|AA|", StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"LIS returned non-AA ACK: {ack.Replace('\r', ' ')}");
        }

        return ack;
    }

    private static async Task<string> ReadMllpFrameAsync(NetworkStream stream, int timeoutMs, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[2048];
        var inFrame = false;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        while (true)
        {
            var read = await stream.ReadAsync(buffer, linked.Token);
            if (read == 0)
            {
                break;
            }

            for (var i = 0; i < read; i++)
            {
                var current = buffer[i];
                if (!inFrame)
                {
                    if (current == Sb)
                    {
                        inFrame = true;
                    }

                    continue;
                }

                if (current == Eb)
                {
                    return Encoding.UTF8.GetString(ms.ToArray());
                }

                ms.WriteByte(current);
            }
        }

        throw new IOException("Did not receive a full MLLP ACK frame before stream closed.");
    }
}
