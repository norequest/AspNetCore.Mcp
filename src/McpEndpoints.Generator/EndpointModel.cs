using McpEndpoints.Generator.Internal;

namespace McpEndpoints.Generator;

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
    LocationInfo? Location)
{
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}
