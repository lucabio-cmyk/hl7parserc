using ClosedXML.Excel;
using Hl7Bridge.Service.Core;

namespace Hl7Bridge.Service.Excel;

public sealed class ClosedXmlTargetParser : IExcelTargetParser
{
    private const string TargetSheetName = "Target";
    private static readonly string[] RequiredSheets = ["Result", "DetailResult", "PositiveNegativeNumber", "Target"];
    private static readonly string[] RequiredHeaders =
    [
        "Sample",
        "Reaction Solution",
        "Fluorescence Channels",
        "Target",
        "Copies(copies/μL)",
        "AnalysisResult"
    ];

    public Task<ParsedWorkbook> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(filePath);

        ValidateSheets(workbook);

        var targetSheet = workbook.Worksheet(TargetSheetName);
        var headers = ReadHeaderIndexes(targetSheet);

        var rows = new List<TargetResultRow>();
        var rowNumber = 3; // row1 title, row2 headers

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sample = targetSheet.Cell(rowNumber, headers["Sample"]).GetString().Trim();
            var target = targetSheet.Cell(rowNumber, headers["Target"]).GetString().Trim();

            if (string.IsNullOrWhiteSpace(sample) && string.IsNullOrWhiteSpace(target))
            {
                break;
            }

            var reaction = targetSheet.Cell(rowNumber, headers["Reaction Solution"]).GetString().Trim();
            var channel = targetSheet.Cell(rowNumber, headers["Fluorescence Channels"]).GetString().Trim();
            var copiesRaw = targetSheet.Cell(rowNumber, headers["Copies(copies/μL)"]).GetString().Trim();
            var interpretation = targetSheet.Cell(rowNumber, headers["AnalysisResult"]).GetString().Trim();

            decimal? copies = null;
            if (!string.IsNullOrWhiteSpace(copiesRaw) && decimal.TryParse(copiesRaw, out var parsed))
            {
                copies = parsed;
            }

            if (!string.IsNullOrWhiteSpace(sample) && !string.IsNullOrWhiteSpace(target))
            {
                rows.Add(new TargetResultRow(
                    sample,
                    reaction,
                    channel,
                    target,
                    copies,
                    interpretation,
                    rowNumber));
            }

            rowNumber++;
        }

        if (rows.Count == 0)
        {
            throw new InvalidDataException("Target sheet contains no data rows for export.");
        }

        var grouped = rows
            .GroupBy(r => r.Sample, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SampleResult(
                group.Key,
                group.Select(r => r.ReactionSolution).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty,
                group.OrderBy(r => r.SourceRowNumber).ToList(),
                Path.GetFileName(filePath)))
            .OrderBy(s => s.Sample, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(new ParsedWorkbook(filePath, grouped));
    }

    private static void ValidateSheets(XLWorkbook workbook)
    {
        foreach (var required in RequiredSheets)
        {
            if (!workbook.TryGetWorksheet(required, out _))
            {
                throw new InvalidDataException($"Missing required worksheet '{required}'.");
            }
        }
    }

    private static Dictionary<string, int> ReadHeaderIndexes(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = sheet.Row(2);
        var lastCell = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var col = 1; col <= lastCell; col++)
        {
            var header = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(header))
            {
                map[header] = col;
            }
        }

        var missing = RequiredHeaders.Where(h => !map.ContainsKey(h)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException($"Target sheet missing required headers: {string.Join(", ", missing)}");
        }

        return map;
    }
}
