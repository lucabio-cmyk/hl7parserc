using Hl7Bridge.Service.Core;

namespace Hl7Bridge.Service.Excel;

public interface IExcelTargetParser
{
    Task<ParsedWorkbook> ParseAsync(string filePath, CancellationToken cancellationToken);
}
