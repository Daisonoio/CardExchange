using CardExchange.API.Services.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    /// <summary>Metriche del piano di lancio per gli amministratori, per città.</summary>
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Route("api/admin/metrics")]
    public class AdminMetricsController : ControllerBase
    {
        private readonly PlanMetrics _metrics;

        public AdminMetricsController(PlanMetrics metrics) => _metrics = metrics;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? city, [FromQuery] int weeks = 8, CancellationToken ct = default) =>
            Ok(await _metrics.ComputeAsync(city, DateTime.UtcNow, Math.Clamp(weeks, 1, 26), ct));
    }
}
