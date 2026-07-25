using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Catalog.API.HealthChecks
{
    /// <summary>
    /// Produces a structured JSON response for health-check endpoints.
    /// Matches the format expected by legacy monitoring agents, APM tools,
    /// Kubernetes liveness / readiness probes, and load balancer health checks.
    ///
    /// Response shape:
    /// {
    ///   "status":      "Healthy" | "Degraded" | "Unhealthy",
    ///   "timestamp":   "2025-01-01T00:00:00.000Z",
    ///   "duration":    "00:00:00.123",
    ///   "entries": {
    ///     "check_name": {
    ///       "status":      "Healthy",
    ///       "description": "...",
    ///       "duration":    "00:00:00.012",
    ///       "data":        { ... },
    ///       "tags":        [ "ready" ]
    ///     }
    ///   }
    /// }
    /// </summary>
    public static class HealthCheckResponseWriter
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented          = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters             = { new JsonStringEnumConverter() }
        };

        public static async Task WriteDetailedJson(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new HealthEntryDto
                {
                    Status      = e.Value.Status.ToString(),
                    Description = e.Value.Description,
                    Duration    = e.Value.Duration.ToString(@"hh\:mm\:ss\.fff"),
                    Error       = e.Value.Exception?.Message,
                    Data        = e.Value.Data.Count > 0
                                    ? e.Value.Data.ToDictionary(d => d.Key, d => d.Value)
                                    : null,
                    Tags        = e.Value.Tags.Any() ? e.Value.Tags.ToArray() : null
                });

            var payload = new HealthReportDto
            {
                Status    = report.Status.ToString(),
                Timestamp = DateTime.UtcNow.ToString("o"),
                Duration  = report.TotalDuration.ToString(@"hh\:mm\:ss\.fff"),
                Entries   = entries
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(payload, _options));
        }

        /// <summary>
        /// Options factory helper — wires detailed JSON output and filters by tag.
        /// </summary>
        public static HealthCheckOptions DetailedOptions(params string[] tags) =>
            new()
            {
                Predicate             = tags.Length == 0
                                          ? _ => true
                                          : hc => hc.Tags.Any(tags.Contains),
                ResponseWriter        = WriteDetailedJson,
                AllowCachingResponses = false
            };

        // Concrete DTO types replace anonymous types to avoid CS0518.
        // The compiler generates a hidden class per anonymous type that inherits
        // System.Object; in complex lambda/LINQ contexts the IDE analyzer can
        // fail to resolve that chain and reports CS0518 across the project.

        private sealed class HealthEntryDto
        {
            [JsonPropertyName("status")]
            public string Status { get; init; } = string.Empty;

            [JsonPropertyName("description")]
            public string? Description { get; init; }

            [JsonPropertyName("duration")]
            public string Duration { get; init; } = string.Empty;

            [JsonPropertyName("error")]
            public string? Error { get; init; }

            [JsonPropertyName("data")]
            public Dictionary<string, object>? Data { get; init; }

            [JsonPropertyName("tags")]
            public string[]? Tags { get; init; }
        }

        private sealed class HealthReportDto
        {
            [JsonPropertyName("status")]
            public string Status { get; init; } = string.Empty;

            [JsonPropertyName("timestamp")]
            public string Timestamp { get; init; } = string.Empty;

            [JsonPropertyName("duration")]
            public string Duration { get; init; } = string.Empty;

            [JsonPropertyName("entries")]
            public Dictionary<string, HealthEntryDto> Entries { get; init; } = new();
        }
    }
}
