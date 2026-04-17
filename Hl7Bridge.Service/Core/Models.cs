namespace Hl7Bridge.Service.Core;

public sealed record TargetResultRow(
    string Sample,
    string ReactionSolution,
    string FluorescenceChannels,
    string Target,
    decimal? CopiesPerMicroliter,
    string AnalysisResult,
    int SourceRowNumber);

public sealed record SampleResult(
    string Sample,
    string ReactionSolution,
    IReadOnlyList<TargetResultRow> Targets,
    string SourceFileName);

public sealed record ParsedWorkbook(
    string FilePath,
    IReadOnlyList<SampleResult> Samples);

public sealed record Hl7DispatchItem(
    SampleResult Sample,
    string MessageControlId,
    string Hl7Payload);
