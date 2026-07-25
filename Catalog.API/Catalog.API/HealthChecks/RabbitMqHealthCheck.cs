using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Catalog.API.HealthChecks
{
    /// <summary>
    /// Readiness check — verifies connectivity to the RabbitMQ message broker.
    /// A failed check means the app cannot publish or consume domain events.
    /// </summary>
    public class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly ConnectionFactory _factory;

        public RabbitMqHealthCheck(ConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = await _factory.CreateConnectionAsync(cancellationToken);
                await using var channel   = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

                var data = new Dictionary<string, object>
                {
                    ["host"]      = _factory.HostName ?? "unknown",
                    ["connected"] = connection.IsOpen
                };

                return connection.IsOpen
                    ? HealthCheckResult.Healthy("RabbitMQ broker is reachable.", data)
                    : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.", data: data);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    "RabbitMQ broker is unreachable.",
                    ex,
                    new Dictionary<string, object> { ["host"] = _factory.HostName ?? "unknown" });
            }
        }
    }
}
