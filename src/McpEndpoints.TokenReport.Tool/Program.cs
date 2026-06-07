using McpEndpoints.TokenReport;

const string usage = """
mcp-token-report - offline MCP tools/list token-cost analyzer

Usage:
  mcp-token-report <path-to-tools-list.json> [--markdown] [--budget N]

Options:
  --markdown      Render the report as Markdown instead of plain text.
  --budget N      Fail (exit 1) when the total token cost exceeds N tokens.

Token counts are an OFFLINE heuristic estimate (no network, ~±20%).
""";

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(usage);
    return args.Length == 0 ? 1 : 0;
}

var path = args[0];
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

if (!File.Exists(path))
{
    Console.Error.WriteLine($"error: file not found: {path}");
    return 1;
}

string json;
try
{
    json = File.ReadAllText(path);
}
catch (IOException ex)
{
    Console.Error.WriteLine($"error: could not read file: {ex.Message}");
    return 1;
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
