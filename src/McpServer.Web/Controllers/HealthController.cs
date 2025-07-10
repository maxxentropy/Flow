using McpServer.Domain.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.Web.Controllers;

/// <summary>
/// Controller for health checks.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthController"/> class.
    /// </summary>
    public HealthController(
        IHealthCheckService healthCheckService,
        ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the overall health status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResult), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetHealth()
    {
        var result = await _healthCheckService.CheckHealthAsync();
        
        if (result.Status == HealthStatus.Unhealthy)
        {
            return StatusCode(503, result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Gets a simple health check for load balancers.
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(200)]
    public IActionResult GetLiveness()
    {
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Gets readiness status.
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetReadiness()
    {
        var result = await _healthCheckService.CheckHealthAsync();
        
        if (result.Status == HealthStatus.Unhealthy)
        {
            return StatusCode(503, new { status = "not_ready", timestamp = DateTime.UtcNow });
        }
        
        return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Gets the health status of a specific component.
    /// </summary>
    [HttpGet("component/{componentName}")]
    [ProducesResponseType(typeof(ComponentHealthResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetComponentHealth(string componentName)
    {
        var result = await _healthCheckService.CheckComponentAsync(componentName);
        
        if (result.Error?.Contains("not found") == true)
        {
            return NotFound(result);
        }
        
        if (result.Status == HealthStatus.Unhealthy)
        {
            return StatusCode(503, result);
        }
        
        return Ok(result);
    }
}