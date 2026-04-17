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
        lock (_sync)
        {
            var state = LoadLocked();
            return state.Any(i => i.Hash == hash);
        }
    }

    public void MarkProcessed(string filePath)
    {
        var hash = ComputeHash(filePath);
        lock (_sync)
        {
            var state = LoadLocked();
            if (state.All(i => i.Hash != hash))
            {
                state.Add(new FingerprintEntry(hash, DateTime.UtcNow));
            }

            var threshold = DateTime.UtcNow.AddDays(-_retentionDays);
            state = state.Where(i => i.ProcessedUtc >= threshold).ToList();
            SaveLocked(state);
        }
    }

    private List<FingerprintEntry> LoadLocked()
    {
        if (!File.Exists(_stateFilePath))
        {
            return [];
        }

        var json = File.ReadAllText(_stateFilePath);
        return JsonSerializer.Deserialize<List<FingerprintEntry>>(json) ?? [];
    }

    private void SaveLocked(List<FingerprintEntry> state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_stateFilePath, json);
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
