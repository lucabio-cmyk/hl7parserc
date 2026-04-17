using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hl7Bridge.Service.Configuration;

public sealed class BridgeConfigurationStore(IHostEnvironment hostEnvironment)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _appSettingsPath = Path.Combine(hostEnvironment.ContentRootPath, "appsettings.json");

    public BridgeOptions LoadCurrent()
    {
        var root = JsonNode.Parse(File.ReadAllText(_appSettingsPath))?.AsObject()
                   ?? throw new InvalidOperationException("Unable to parse appsettings.json.");
        var bridgeNode = root["Bridge"] ?? throw new InvalidOperationException("Bridge section not found in appsettings.json.");

        return bridgeNode.Deserialize<BridgeOptions>(JsonOptions)
               ?? throw new InvalidOperationException("Unable to deserialize Bridge configuration.");
    }

    public ValidationResultDto Save(BridgeOptions options)
    {
        var validationErrors = Validate(options);
        if (validationErrors.Count != 0)
        {
            return new ValidationResultDto(false, validationErrors);
        }

        var root = JsonNode.Parse(File.ReadAllText(_appSettingsPath))?.AsObject()
                   ?? throw new InvalidOperationException("Unable to parse appsettings.json.");

        root["Bridge"] = JsonSerializer.SerializeToNode(options, JsonOptions);
        File.WriteAllText(_appSettingsPath, root.ToJsonString(JsonOptions));

        return new ValidationResultDto(true, []);
    }

    private static List<string> Validate(object value)
    {
        var errors = new List<string>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ValidateRecursive(value, errors, visited, value.GetType().Name);
        return errors;
    }

    private static void ValidateRecursive(object value, List<string> errors, HashSet<object> visited, string path)
    {
        if (visited.Contains(value))
        {
            return;
        }

        visited.Add(value);

        var context = new ValidationContext(value);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, context, results, validateAllProperties: true);

        foreach (var result in results)
        {
            errors.Add($"{path}: {result.ErrorMessage}");
        }

        var properties = value.GetType().GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToList();

        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(value);
            if (propertyValue is null || property.PropertyType == typeof(string) || property.PropertyType.IsPrimitive)
            {
                continue;
            }

            if (propertyValue is System.Collections.IDictionary dictionary)
            {
                foreach (var key in dictionary.Keys)
                {
                    var item = dictionary[key];
                    if (item is null)
                    {
                        continue;
                    }

                    ValidateRecursive(item, errors, visited, $"{path}.{property.Name}[{key}]");
                }

                continue;
            }

            if (propertyValue is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    ValidateRecursive(item, errors, visited, $"{path}.{property.Name}");
                }

                continue;
            }

            ValidateRecursive(propertyValue, errors, visited, $"{path}.{property.Name}");
        }
    }
}

public sealed record ValidationResultDto(bool IsValid, IReadOnlyList<string> Errors);

public sealed record BridgeStatusDto(
    string ServiceUtcTime,
    int IncomingFiles,
    int ProcessingFiles,
    int SentFiles,
    int ErrorFiles,
    string LisHost,
    int LisPort,
    bool Hl7ArchiveEnabled);
