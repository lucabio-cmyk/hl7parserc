namespace Hl7Bridge.Service.State;

public interface IFileStateManager
{
    string ClaimForProcessing(string incomingPath);
    void MarkSent(string processingPath);
    void MarkError(string processingPath, Exception exception);
    bool IsFileStable(string filePath, TimeSpan stableDuration, TimeSpan probeInterval, CancellationToken cancellationToken);
    void ArchiveHl7(string sourceFileName, string sample, string hl7Payload);
}
