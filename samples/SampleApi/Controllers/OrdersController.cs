using McpEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace SampleApi.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    /// <summary>Gets an order by its id.</summary>
    [HttpGet("{id}")]
    [McpTool(Name = "getOrder")]
    public string GetOrder(int id) => $"order-{id}";
}
