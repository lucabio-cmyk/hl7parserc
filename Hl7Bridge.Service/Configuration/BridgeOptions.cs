using System.ComponentModel.DataAnnotations;

namespace Hl7Bridge.Service.Configuration;

public sealed class BridgeOptions
{
    [Required] public FolderOptions Folders { get; init; } = new();
    [Required] public LisOptions Lis { get; init; } = new();
    [Required] public Hl7Options Hl7 { get; init; } = new();
    [Required] public ProcessingOptions Processing { get; init; } = new();
}

public sealed class FolderOptions
{
    [Required] public string Incoming { get; init; } = @"C:\Hl7Bridge\incoming";
    [Required] public string Processing { get; init; } = @"C:\Hl7Bridge\processing";
    [Required] public string Sent { get; init; } = @"C:\Hl7Bridge\sent";
    [Required] public string Error { get; init; } = @"C:\Hl7Bridge\error";
    [Required] public string Logs { get; init; } = @"C:\Hl7Bridge\logs";
    public bool Hl7ArchiveEnabled { get; init; } = true;
    public string Hl7Archive { get; init; } = @"C:\Hl7Bridge\hl7_archive";
}

public sealed class LisOptions
{
    [Required] public string Host { get; init; } = "127.0.0.1";
    [Range(1, 65535)] public int Port { get; init; } = 2575;
    [Range(1, 10)] public int RetryCount { get; init; } = 3;
    [Range(100, 60000)] public int RetryIntervalMs { get; init; } = 2000;
    [Range(1000, 120000)] public int AckTimeoutMs { get; init; } = 10000;
}

public sealed class Hl7Options
{
    [Required] public string Version { get; init; } = "2.5.1";
    [Required] public string SendingApplication { get; init; } = "INSTRUMENT_BRIDGE";
    [Required] public string SendingFacility { get; init; } = "LAB";
    [Required] public string ReceivingApplication { get; init; } = "LIS";
    [Required] public string ReceivingFacility { get; init; } = "MAIN";
    [Required] public string ProcessingId { get; init; } = "P";
    public string PlaceholderPatientIdPrefix { get; init; } = "SAMPLE";
    [Required] public Dictionary<string, TargetMapEntry> TargetCodeMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TargetMapEntry
{
    [Required] public string Code { get; init; } = string.Empty;
    [Required] public string CodingSystem { get; init; } = "99LOCAL";
    public string ValueTypeForCopies { get; init; } = "NM";
    public string Units { get; init; } = "copies/uL";
}

public sealed class ProcessingOptions
{
    [Range(500, 60000)] public int PollIntervalMs { get; init; } = 2000;
    [Range(500, 600000)] public int StableFileWaitMs { get; init; } = 5000;
    [Range(100, 60000)] public int StableFileProbeIntervalMs { get; init; } = 1000;
    [Range(1, 100)] public int MaxFilesPerCycle { get; init; } = 10;
    [Range(1, 365)] public int DuplicateRetentionDays { get; init; } = 30;
}
