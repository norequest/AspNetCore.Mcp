using System;
using System.Text.Json;

namespace McpEndpoints;

/// <summary>
/// Best-effort shaping of tool output: projects to selected top-level fields and/or
/// truncates to a maximum length. Never throws; on any parse failure it falls back to
/// the original string. Projection happens first, then truncation.
/// </summary>
public static class OutputShaper
{
    public static string Shape(string json, int? maxLength, string[]? fields)
    {
        var result = json;

        if (fields is { Length: > 0 })
            result = Project(json, fields);

        if (maxLength is { } max && max >= 0 && result.Length > max)
            result = result.Substring(0, max);

        return result;
    }

    private static string Project(string json, string[] fields)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var buffer = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                if (root.ValueKind == JsonValueKind.Object)
                {
                    WriteProjectedObject(writer, root, fields);
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    writer.WriteStartArray();
                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Object)
                            WriteProjectedObject(writer, element, fields);
                        else
                            element.WriteTo(writer);
                    }
                    writer.WriteEndArray();
                }
                else
                {
                    // Not an object or array; nothing to project.
                    return json;
                }
            }

            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch
        {
            // Best-effort: on any parse/serialization error, return the original.
            return json;
        }
    }

    private static void WriteProjectedObject(Utf8JsonWriter writer, JsonElement obj, string[] fields)
    {
        writer.WriteStartObject();
        foreach (var name in fields)
        {
            if (obj.TryGetProperty(name, out var value))
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }
}
