namespace Hl7Bridge.Service.State;

public interface IDuplicateGuard
{
    bool IsDuplicate(string filePath);
    void MarkProcessed(string filePath);
}
