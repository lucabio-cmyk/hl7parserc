using Hl7Bridge.Service.Configuration;
using Hl7Bridge.Service.Processing;
using Hl7Bridge.Service.State;
using Microsoft.Extensions.Options;

namespace Hl7Bridge.Service;

public sealed class InstrumentBridgeWorker(
    IOptions<BridgeOptions> options,
    IFileStateManager stateManager,
    ISampleDispatchService dispatchService,
    ILogger<InstrumentBridgeWorker> logger) : BackgroundService
{
    private readonly BridgeOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HL7 bridge worker started. Monitoring {Folder}", _options.Folders.Incoming);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug("Starting processing cycle.");
                var files = Directory
                    .EnumerateFiles(_options.Folders.Incoming, "*.xlsx", SearchOption.TopDirectoryOnly)
                    .OrderBy(File.GetCreationTimeUtc)
                    .Take(_options.Processing.MaxFilesPerCycle)
                    .ToList();
                logger.LogDebug("Cycle discovered {Count} file(s).", files.Count);

                foreach (var incoming in files)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    logger.LogInformation("Processing incoming file {File}.", incoming);
                    await ProcessIncomingAsync(incoming, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cycle level failure in worker loop.");
            }

            await Task.Delay(_options.Processing.PollIntervalMs, stoppingToken);
        }

        logger.LogInformation("HL7 bridge worker stopped.");
    }

    private async Task ProcessIncomingAsync(string incomingPath, CancellationToken cancellationToken)
    {
        if (!stateManager.IsFileStable(
                incomingPath,
                TimeSpan.FromMilliseconds(_options.Processing.StableFileWaitMs),
                TimeSpan.FromMilliseconds(_options.Processing.StableFileProbeIntervalMs),
                cancellationToken))
        {
            logger.LogDebug("File not yet stable: {File}", incomingPath);
            return;
        }

        string? processingPath = null;
        try
        {
            processingPath = stateManager.ClaimForProcessing(incomingPath);
            await dispatchService.ProcessFileAsync(processingPath, cancellationToken);
            stateManager.MarkSent(processingPath);
            logger.LogInformation("Processing completed for file {File}.", incomingPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Processing failed for file {File}", incomingPath);
            if (!string.IsNullOrWhiteSpace(processingPath) && File.Exists(processingPath))
            {
                stateManager.MarkError(processingPath, ex);
            }
        }
    }
}
