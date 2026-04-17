namespace Hl7Bridge.Service.Processing;

public interface ISampleDispatchService
{
    Task ProcessFileAsync(string processingPath, CancellationToken cancellationToken);
}
