using Hl7Bridge.Service.Configuration;
using Microsoft.Extensions.Options;

namespace Hl7Bridge.Service.State;

public sealed class FileStateManager(IOptions<BridgeOptions> options, ILogger<FileStateManager> logger) : IFileStateManager
{
    private readonly BridgeOptions _options = options.Value;
    private readonly ILogger<FileStateManager> _logger = logger;

    public string ClaimForProcessing(string incomingPath)
    {
        var destination = Path.Combine(_options.Folders.Processing, Path.GetFileName(incomingPath));
        File.Move(incomingPath, destination, overwrite: true);
        _logger.LogInformation("Claimed file for processing: {Source} -> {Destination}", incomingPath, destination);
        return destination;
    }

    public void MarkSent(string processingPath)
    {
        var destination = Path.Combine(_options.Folders.Sent, Path.GetFileName(processingPath));
        File.Move(processingPath, destination, overwrite: true);
        _logger.LogInformation("Moved file to sent: {File}", destination);
    }

    public void MarkError(string processingPath, Exception exception)
    {
        var destination = Path.Combine(_options.Folders.Error, Path.GetFileName(processingPath));
        File.Move(processingPath, destination, overwrite: true);
        File.WriteAllText(destination + ".error.txt", exception.ToString());
        _logger.LogError(exception, "Moved file to error: {File}", destination);
    }

    public bool IsFileStable(string filePath, TimeSpan stableDuration, TimeSpan probeInterval, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking file stability for {File}.", filePath);
        var stableUntil = DateTime.UtcNow + stableDuration;
        long lastLength = -1;
        DateTime lastWrite = DateTime.MinValue;

        while (DateTime.UtcNow < stableUntil)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new FileInfo(filePath);
            if (!info.Exists)
            {
                _logger.LogWarning("File disappeared during stability check: {File}", filePath);
                return false;
            }

            if (lastLength != info.Length || lastWrite != info.LastWriteTimeUtc)
            {
                lastLength = info.Length;
                lastWrite = info.LastWriteTimeUtc;
                stableUntil = DateTime.UtcNow + stableDuration;
            }

            Thread.Sleep(probeInterval);
        }

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            _logger.LogDebug("File is stable and unlocked: {File}.", filePath);
            return stream.Length >= 0;
        }
        catch
        {
            _logger.LogDebug("File still locked/not stable: {File}.", filePath);
            return false;
        }
    }

    public void ArchiveHl7(string sourceFileName, string sample, string hl7Payload)
    {
        if (!_options.Folders.Hl7ArchiveEnabled)
        {
            return;
        }

        var safeSample = string.Concat(sample.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var outName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Path.GetFileNameWithoutExtension(sourceFileName)}_{safeSample}.hl7";
        var fullPath = Path.Combine(_options.Folders.Hl7Archive, outName);
        File.WriteAllText(fullPath, hl7Payload);
        _logger.LogInformation("Archived HL7 payload for sample {Sample} to {Path}.", sample, fullPath);
    }
}
