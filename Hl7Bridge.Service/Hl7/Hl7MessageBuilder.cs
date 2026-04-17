using System.Text;
using Hl7Bridge.Service.Configuration;
using Hl7Bridge.Service.Core;
using Microsoft.Extensions.Options;

namespace Hl7Bridge.Service.Hl7;

public sealed class Hl7MessageBuilder(IOptions<BridgeOptions> options) : IHl7MessageBuilder
{
    private readonly BridgeOptions _options = options.Value;

    public string BuildOruR01(SampleResult sample, string messageControlId)
    {
        var now = DateTime.UtcNow;
        var dtm = now.ToString("yyyyMMddHHmmss");
        var sb = new StringBuilder();

        var msh = $"MSH|^~\\&|{Esc(_options.Hl7.SendingApplication)}|{Esc(_options.Hl7.SendingFacility)}|{Esc(_options.Hl7.ReceivingApplication)}|{Esc(_options.Hl7.ReceivingFacility)}|{dtm}||ORU^R01^ORU_R01|{Esc(messageControlId)}|{Esc(_options.Hl7.ProcessingId)}|{Esc(_options.Hl7.Version)}";
        var pid = $"PID|1||{Esc(_options.Hl7.PlaceholderPatientIdPrefix)}-{Esc(sample.Sample)}||{Esc(sample.Sample)}";
        var obr = $"OBR|1||{Esc(sample.Sample)}|INSTRUMENT_PANEL^{Esc(sample.ReactionSolution)}^99LOCAL|{dtm}|||||||||||{Esc(sample.Sample)}";

        sb.Append(msh).Append('\r');
        sb.Append(pid).Append('\r');
        sb.Append(obr).Append('\r');

        var index = 1;
        foreach (var row in sample.Targets)
        {
            var map = ResolveMap(row.Target);
            var identifier = $"{Esc(map.Code)}^{Esc(row.Target)}^{Esc(map.CodingSystem)}";

            var copiesValue = row.CopiesPerMicroliter.HasValue ? row.CopiesPerMicroliter.Value.ToString("0.###") : string.Empty;
            var interpretation = NormalizeInterpretation(row.AnalysisResult);

            // OBX #1: Quantitative copies value.
            sb.Append($"OBX|{index++}|{Esc(map.ValueTypeForCopies)}|{identifier}|1|{Esc(copiesValue)}|{Esc(map.Units)}|||||F|||{dtm}").Append('\r');
            // OBX #2: Qualitative positive/negative interpretation.
            sb.Append($"OBX|{index++}|CWE|{identifier}-INT^Interpretation^99LOCAL|1|{Esc(interpretation)}^{Esc(interpretation)}^HL70078|||||F|||{dtm}").Append('\r');
        }

        return sb.ToString();
    }

    private TargetMapEntry ResolveMap(string targetName)
    {
        if (_options.Hl7.TargetCodeMap.TryGetValue(targetName, out var entry))
        {
            return entry;
        }

        return new TargetMapEntry
        {
            Code = targetName.Replace(' ', '_').ToUpperInvariant(),
            CodingSystem = "99LOCAL",
            Units = "copies/uL",
            ValueTypeForCopies = "NM"
        };
    }

    private static string NormalizeInterpretation(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "POSITIVE" => "POS",
            "NEGATIVE" => "NEG",
            _ => string.IsNullOrWhiteSpace(value) ? "UNK" : value.Trim().ToUpperInvariant()
        };
    }

    private static string Esc(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input
            .Replace("\\", "\\E\\", StringComparison.Ordinal)
            .Replace("|", "\\F\\", StringComparison.Ordinal)
            .Replace("^", "\\S\\", StringComparison.Ordinal)
            .Replace("~", "\\R\\", StringComparison.Ordinal)
            .Replace("&", "\\T\\", StringComparison.Ordinal);
    }
}
