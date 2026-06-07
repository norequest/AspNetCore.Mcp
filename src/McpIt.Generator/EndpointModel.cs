using McpIt.Generator.Internal;

namespace McpIt.Generator;

public enum ParameterSource { Route, Query, Body }

public sealed record ParameterModel(
    string Name,
    string TypeFullyQualified,
    ParameterSource Source);

public sealed record EndpointModel(
    string Namespace,
    string GeneratedClassName,
    string ToolName,
    string? Description,
    string HttpMethod,
    string RouteTemplate,
    EquatableArray<ParameterModel> Parameters,
    bool ReadOnly,
    bool Destructive,
    bool Idempotent,
    bool AllowDestructive,
    int? OutputMaxLength,
    EquatableArray<string> OutputFields,
    LocationInfo? Location)
{
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasOutputShaping => OutputMaxLength.HasValue || OutputFields.Count > 0;
}
