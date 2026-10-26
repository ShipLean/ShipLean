using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests.Telemetry;

// Writes one log inside the request. Top level because MVC's controller discovery skips nested types; only loaded by
// TelemetryLogsTests, which adds this assembly as an application part of its own test host.
[Route("api/test-controller/log-probe")]
public sealed class LogProbeTestController(ILogger<LogProbeTestController> logger) : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        logger.LogWarning("probe inside request");
        return Ok(System.Diagnostics.Activity.Current?.TraceId.ToHexString() ?? "none");
    }
}
