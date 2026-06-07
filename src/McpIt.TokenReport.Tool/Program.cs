using System.Net.Http;
using System.Text;
using McpIt.TokenReport;

const string usage = """
mcp-token-report - offline MCP tools/list token-cost analyzer

Usage:
  mcp-token-report <source> [--markdown] [--budget N]

<source> is either:
  - a path to a tools/list JSON file, or
  - a URL to a running MCP server endpoint (e.g. http://localhost:5199/mcp),
    which is queried with a tools/list request.

Options:
  --markdown      Render the report as Markdown instead of plain text.
  --budget N      Fail (exit 1) when the total token cost exceeds N tokens.

Token counts are an OFFLINE heuristic estimate (no network for tokenizing, ~±20%).
""";

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(usage);
    return args.Length == 0 ? 1 : 0;
}

var source = args[0];
var markdown = false;
int? budget = null;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--markdown":
            markdown = true;
            break;
        case "--budget":
            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var b))
            {
                Console.Error.WriteLine("error: --budget requires an integer argument.");
                return 1;
            }
            budget = b;
            i++;
            break;
        default:
            Console.Error.WriteLine($"error: unknown argument '{args[i]}'.");
            Console.Error.WriteLine(usage);
            return 1;
    }
}

string json;
var isUrl = source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
         || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

if (isUrl)
{
    try
    {
        json = await FetchToolsListAsync(source);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: could not query MCP server '{source}': {ex.Message}");
        return 1;
    }
}
else
{
    if (!File.Exists(source))
    {
        Console.Error.WriteLine($"error: file not found: {source}");
        return 1;
    }

    try
    {
        json = File.ReadAllText(source);
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"error: could not read file: {ex.Message}");
        return 1;
    }
}

IReadOnlyList<ToolDescriptor> tools;
try
{
    tools = ToolListParser.Parse(json);
}
catch (System.Text.Json.JsonException ex)
{
    Console.Error.WriteLine($"error: invalid JSON: {ex.Message}");
    return 1;
}

var report = TokenReporter.Analyze(tools, new HeuristicTokenizer());

Console.WriteLine(markdown ? ReportRenderer.RenderMarkdown(report) : ReportRenderer.RenderText(report));

if (TokenReporter.ExceedsBudget(report, budget))
{
    Console.Error.WriteLine(
        $"error: token budget exceeded: {report.TotalTokens} tokens > budget of {budget} tokens.");
    return 1;
}

return 0;

// Queries a running MCP server for its tool list and returns the JSON payload.
// Handles both a plain JSON response and a Server-Sent-Events (text/event-stream) response.
static async Task<string> FetchToolsListAsync(string url)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    using var request = new HttpRequestMessage(HttpMethod.Post, url);
    request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
    request.Content = new StringContent(
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}",
        Encoding.UTF8,
        "application/json");

    using var response = await http.SendAsync(request);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync();
    return ExtractJsonPayload(body);
}

// SSE bodies look like "event: message\ndata: {json}\n\n"; pull the JSON out of the data line(s).
static string ExtractJsonPayload(string body)
{
    if (body.TrimStart().StartsWith('{'))
        return body;

    var sb = new StringBuilder();
    foreach (var rawLine in body.Split('\n'))
    {
        var line = rawLine.TrimEnd('\r');
        if (line.StartsWith("data:", StringComparison.Ordinal))
            sb.Append(line.Substring("data:".Length).TrimStart());
    }
    return sb.ToString();
}
