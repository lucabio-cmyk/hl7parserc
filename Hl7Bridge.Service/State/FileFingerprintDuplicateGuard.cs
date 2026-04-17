using System.Security.Cryptography;
using System.Text.Json;
using Hl7Bridge.Service.Configuration;
using Microsoft.Extensions.Options;

namespace Hl7Bridge.Service.State;

public sealed class FileFingerprintDuplicateGuard(IOptions<BridgeOptions> options) : IDuplicateGuard
{
    private readonly string _stateFilePath = Path.Combine(options.Value.Folders.Logs, "processed_fingerprints.json");
    private readonly int _retentionDays = options.Value.Processing.DuplicateRetentionDays;
    private readonly object _sync = new();

    public bool IsDuplicate(string filePath)
    {
        var hash = ComputeHash(filePath);
        var state = Load();
        return state.Any(i => i.Hash == hash);
    }

    public void MarkProcessed(string filePath)
    {
        var hash = ComputeHash(filePath);
        var state = Load();
        if (state.All(i => i.Hash != hash))
        {
            state.Add(new FingerprintEntry(hash, DateTime.UtcNow));
        }

        var threshold = DateTime.UtcNow.AddDays(-_retentionDays);
        state = state.Where(i => i.ProcessedUtc >= threshold).ToList();
        Save(state);
    }

    private List<FingerprintEntry> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_stateFilePath))
            {
                return [];
            }

            var json = File.ReadAllText(_stateFilePath);
            return JsonSerializer.Deserialize<List<FingerprintEntry>>(json) ?? [];
        }
    }

    private void Save(List<FingerprintEntry> state)
    {
        lock (_sync)
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_stateFilePath, json);
        }
    }

    private static string ComputeHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private sealed record FingerprintEntry(string Hash, DateTime ProcessedUtc);
}
