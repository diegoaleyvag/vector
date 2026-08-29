using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vector.App.Tests;

internal static class PortableJsonSchemaValidator
{
    public static IReadOnlyList<string> Validate(JsonElement instance, JsonElement schema)
    {
        var errors = new List<string>();
        Validate(instance, schema, "$", errors);
        return errors;
    }

    private static void Validate(
        JsonElement instance,
        JsonElement schema,
        string path,
        ICollection<string> errors)
    {
        if (schema.TryGetProperty("oneOf", out var alternatives))
        {
            var matchingAlternatives = 0;
            foreach (var alternative in alternatives.EnumerateArray())
            {
                var alternativeErrors = new List<string>();
                Validate(instance, alternative, path, alternativeErrors);
                if (alternativeErrors.Count == 0)
                {
                    matchingAlternatives++;
                }
            }

            if (matchingAlternatives != 1)
            {
                errors.Add($"{path} matched {matchingAlternatives} oneOf alternatives.");
            }

            return;
        }

        if (schema.TryGetProperty("type", out var type))
        {
            var expectedType = type.GetString();
            if (!MatchesType(instance, expectedType))
            {
                errors.Add($"{path} must be a {expectedType}.");
                return;
            }
        }

        if (schema.TryGetProperty("const", out var constant) &&
            !JsonElement.DeepEquals(instance, constant))
        {
            errors.Add($"{path} must equal {constant.GetRawText()}.");
        }

        if (schema.TryGetProperty("enum", out var allowedValues) &&
            !allowedValues.EnumerateArray().Any(value => JsonElement.DeepEquals(instance, value)))
        {
            errors.Add($"{path} has a value outside the schema enum.");
        }

        if (instance.ValueKind == JsonValueKind.String)
        {
            var value = instance.GetString()!;
            if (schema.TryGetProperty("minLength", out var minimumLength) &&
                value.Length < minimumLength.GetInt32())
            {
                errors.Add($"{path} is shorter than the schema minimum.");
            }

            if (schema.TryGetProperty("maxLength", out var maximumLength) &&
                value.Length > maximumLength.GetInt32())
            {
                errors.Add($"{path} is longer than the schema maximum.");
            }

            if (schema.TryGetProperty("pattern", out var pattern) &&
                !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant))
            {
                errors.Add($"{path} does not match the schema pattern.");
            }

            if (schema.TryGetProperty("format", out var format) &&
                format.GetString() == "uri" &&
                !Uri.TryCreate(value, UriKind.Absolute, out _))
            {
                errors.Add($"{path} is not an absolute URI.");
            }
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            var properties = schema.TryGetProperty("properties", out var declaredProperties)
                ? declaredProperties
                : default;

            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var requiredProperty in required.EnumerateArray())
                {
                    if (!instance.TryGetProperty(requiredProperty.GetString()!, out _))
                    {
                        errors.Add($"{path} is missing '{requiredProperty.GetString()}'.");
                    }
                }
            }

            if (schema.TryGetProperty("additionalProperties", out var additionalProperties) &&
                additionalProperties.ValueKind == JsonValueKind.False &&
                properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in instance.EnumerateObject())
                {
                    if (!properties.TryGetProperty(property.Name, out _))
                    {
                        errors.Add($"{path} has unexpected property '{property.Name}'.");
                    }
                }
            }

            if (properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (instance.TryGetProperty(property.Name, out var value))
                    {
                        Validate(value, property.Value, $"{path}.{property.Name}", errors);
                    }
                }
            }
        }

        if (instance.ValueKind == JsonValueKind.Array &&
            schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var item in instance.EnumerateArray())
            {
                Validate(item, items, $"{path}[{index}]", errors);
                index++;
            }
        }
    }

    private static bool MatchesType(JsonElement instance, string? expectedType) =>
        expectedType switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "null" => instance.ValueKind == JsonValueKind.Null,
            _ => false
        };
}
