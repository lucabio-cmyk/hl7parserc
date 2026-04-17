using Hl7Bridge.Service.Core;

namespace Hl7Bridge.Service.Processing;

public interface IResultMapper
{
    IReadOnlyList<Hl7DispatchItem> BuildDispatchItems(ParsedWorkbook workbook);
}
