namespace Hl7Bridge.Service.Infrastructure;

public interface IMllpClient
{
    Task<string> SendAndReceiveAckAsync(string hl7Payload, CancellationToken cancellationToken);
}
