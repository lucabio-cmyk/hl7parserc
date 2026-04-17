using Hl7Bridge.Service.Core;
using Hl7Bridge.Service.Hl7;

namespace Hl7Bridge.Service.Processing;

public sealed class ResultMapper(IHl7MessageBuilder builder) : IResultMapper
{
    public IReadOnlyList<Hl7DispatchItem> BuildDispatchItems(ParsedWorkbook workbook)
    {
        var list = new List<Hl7DispatchItem>();

        foreach (var sample in workbook.Samples)
        {
            var controlId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{sample.Sample}";
            var payload = builder.BuildOruR01(sample, controlId);
            list.Add(new Hl7DispatchItem(sample, controlId, payload));
        }

        return list;
    }
}
