using Hl7Bridge.Service.Core;

namespace Hl7Bridge.Service.Hl7;

public interface IHl7MessageBuilder
{
    string BuildOruR01(SampleResult sample, string messageControlId);
}
