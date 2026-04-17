using Hl7Bridge.Service.Configuration;
using Hl7Bridge.Service.Excel;
using Hl7Bridge.Service.Infrastructure;
using Hl7Bridge.Service.State;
using Microsoft.Extensions.Options;

namespace Hl7Bridge.Service.Processing;

public sealed class SampleDispatchService(
    IExcelTargetParser parser,
    IResultMapper mapper,
    IMllpClient mllpClient,
    IFileStateManager fileStateManager,
    IDuplicateGuard duplicateGuard,
    IOptions<BridgeOptions> options,
    ILogger<SampleDispatchService> logger) : ISampleDispatchService
{
    private readonly BridgeOptions _options = options.Value;

    public async Task ProcessFileAsync(string processingPath, CancellationToken cancellationToken)
    {
        if (duplicateGuard.IsDuplicate(processingPath))
        {
            logger.LogWarning("Skipping duplicate file by fingerprint: {Path}", processingPath);
            return;
        }

        var workbook = await parser.ParseAsync(processingPath, cancellationToken);
        var dispatchItems = mapper.BuildDispatchItems(workbook);

        foreach (var item in dispatchItems)
        {
            var sent = false;
            Exception? last = null;

            for (var attempt = 1; attempt <= _options.Lis.RetryCount; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var ack = await mllpClient.SendAndReceiveAckAsync(item.Hl7Payload, cancellationToken);
                    logger.LogInformation("Sample {Sample} sent with ACK: {Ack}", item.Sample.Sample, ack.Replace('\r', ' '));
                    fileStateManager.ArchiveHl7(item.Sample.SourceFileName, item.Sample.Sample, item.Hl7Payload);
                    sent = true;
                    break;
                }
                catch (Exception ex)
                {
                    last = ex;
                    logger.LogWarning(ex, "Attempt {Attempt}/{Total} failed for sample {Sample}.", attempt, _options.Lis.RetryCount, item.Sample.Sample);
                    if (attempt < _options.Lis.RetryCount)
                    {
                        await Task.Delay(_options.Lis.RetryIntervalMs, cancellationToken);
                    }
                }
            }

            if (!sent)
            {
                throw new IOException($"Failed to send sample {item.Sample.Sample} after retries.", last);
            }
        }

        duplicateGuard.MarkProcessed(processingPath);
    }
}
