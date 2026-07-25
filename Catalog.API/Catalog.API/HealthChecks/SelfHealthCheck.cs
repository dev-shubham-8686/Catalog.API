using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.API.HealthChecks
{
    /// <summary>
    /// Liveness check — confirms the process is running and the DI container
    /// is operational. Used by load balancers / orchestrators to decide
    /// whether to restart the instance.
    /// </summary>
    public class SelfHealthCheck : IHealthCheck
    {
        private static readonly DateTime _startedAt = DateTime.UtcNow;

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var uptime = DateTime.UtcNow - _startedAt;

            var data = new Dictionary<string, object>
            {
                ["uptime"]      = uptime.ToString(@"dd\.hh\:mm\:ss"),
                ["startedAt"]   = _startedAt.ToString("o"),
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                ["machineName"] = Environment.MachineName
            };

            return Task.FromResult(HealthCheckResult.Healthy("Application is running.", data));
        }
    }
}
