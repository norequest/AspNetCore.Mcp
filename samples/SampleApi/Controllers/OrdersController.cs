using McpIt;
using Microsoft.AspNetCore.Mvc;

namespace SampleApi.Controllers;

// A perfectly ordinary ASP.NET Core controller. The only McpIt-specific thing here is
// the [McpTool] / [McpToolOutput] attributes: at BUILD TIME the McpIt source generator
// reads them and emits one MCP tool class per annotated action. There is no hand-written
// MCP server code. At run time each generated tool just loops back to the matching HTTP
// endpoint on this same app, so the REST API and the MCP tools never drift apart.
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    // A tiny in-memory "database" so the demo is self-contained and runnable with no setup.
    private static readonly Order[] Orders =
    [
        new(1, "Ada Lovelace",  "shipped",   129.90m, "1Z-AAA-111", "Difference Engine sticker pack"),
        new(2, "Alan Turing",   "packing",    59.00m, null,         "Enigma-themed mug"),
        new(3, "Grace Hopper",  "delivered", 240.50m, "1Z-CCC-333", "Nanosecond wire (1ft)"),
    ];

    /// <summary>Lists all orders with a short summary line for each.</summary>
    // [McpTool] turns this action into an MCP tool. Because it is an HttpGet, McpIt derives
    // the safety hints automatically from the verb: ReadOnly = true, Destructive = false,
    // Idempotent = true. No name is given, so the tool name is the camelCased method name:
    // "listOrders". The <summary> above becomes the tool's description the model sees.
    [HttpGet]
    [McpTool]
    public IEnumerable<string> ListOrders() =>
        Orders.Select(o => $"#{o.Id} {o.Customer} ({o.Status})");

    /// <summary>Gets the full detail of a single order by its id.</summary>
    // A tool WITH a parameter. The route token {id} becomes a typed MCP tool input, so the
    // model must supply an integer "id". An explicit Name overrides the derived name and is
    // used verbatim, so this tool is "getOrder" (not "getOrderDetail").
    [HttpGet("{id}")]
    [McpTool(Name = "getOrder")]
    public ActionResult<Order> GetOrderDetail(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);
        return order is null ? NotFound() : order;
    }

    /// <summary>Returns just the shipping status and tracking number for an order.</summary>
    // OUTPUT SHAPING. The endpoint returns the whole Order object, but agents rarely need the
    // full payload. [McpToolOutput(Fields = ...)] tells the generated tool to keep ONLY these
    // top-level JSON properties before handing the result to the model, and MaxLength caps the
    // response size. This keeps tool responses small and cheap on tokens without changing the
    // underlying REST API (a browser hitting /orders/1/tracking still gets the full object).
    [HttpGet("{id}/tracking")]
    [McpTool(Name = "getOrderTracking")]
    [McpToolOutput(Fields = new[] { "id", "status", "trackingNumber" }, MaxLength = 400)]
    public ActionResult<Order> GetOrderTracking(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);
        return order is null ? NotFound() : order;
    }
}

// Property names serialize as camelCase by default in ASP.NET Core, which is why the
// [McpToolOutput] Fields above use "trackingNumber" (matching the JSON, not the C# name).
public record Order(
    int Id,
    string Customer,
    string Status,
    decimal Total,
    string? TrackingNumber,
    string Item);
