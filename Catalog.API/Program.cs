using Catalog.API;
using Catalog.API.Extensions;
using Catalog.API.HealthChecks;
using Catalog.API.Middleware;
using Catalog.Domain.Extensions;
using Catalog.Domain.Repositories;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Extensions;
using Catalog.Infrastructure.Repositories;
using Identity.Authentication;
using Identity.Authentication.Data;
using Identity.Authentication.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Polly;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers()
    .AddApplicationPart(typeof(IdentityLibraryMarker).Assembly);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCatalogContext(config.GetSection("DataSource:ConnectionString").Value!);
//builder.Services.AddSqlConnectionFactory(config.GetSection("DataSource:ConnectionString").Value!);

builder.Services.AddIdentityAuthentication(config);
builder.Services.AddJwtAuthentication(config);

builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
});

builder.Services.AddAuthorization(x =>
{
    x.AddPolicy(AuthConstants.AdminUserPolicyName,
        p => p.RequireClaim(AuthConstants.AdminUserClaimName, "true"));

    x.AddPolicy(AuthConstants.TrustedMemberPolicyName,
        p => p.RequireAssertion(c =>
            c.User.HasClaim(m => m is { Type: AuthConstants.AdminUserClaimName, Value: "true" }) ||
            c.User.HasClaim(m => m is { Type: AuthConstants.TrustedMemberClaimName, Value: "true" })));
});

//builder.Services.AddOpenApiDocument(settings => {
//    settings.Title = "Catalog API";
//    settings.DocumentName = "v3";
//    settings.Version = "v3";
//    })

builder.Services
    .AddScoped<IItemRepository, ItemRepository>()
    .AddServices()
    .AddResponseCaching()
    .AddDistributedRedisCache(config);

builder.Services.AddEventBus(config);

builder.Services
    .AddHealthChecks()
    // --- Liveness (tag: live) -----------------------------------
    // Confirms the process is running; orchestrators use this to
    // decide whether to restart the container.
    .AddCheck<SelfHealthCheck>("self", tags: ["live"])
    // --- Readiness (tag: ready) ---------------------------------
    // All external dependencies must be reachable before traffic
    // is routed to this instance.
    .AddSqlServer(
        config.GetSection("DataSource:ConnectionString").Value!,
        name: "sqlserver",
        tags: ["ready"])
    .AddCheck<RedisCacheHealthCheck>("redis", tags: ["ready"])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

void ExecuteMigrations(IApplicationBuilder app, IConfiguration configuration)
{
    var autoMigrate = configuration.GetValue("Database:AutoMigrate", defaultValue: true);
    if (!autoMigrate) return;

    var retry = Policy.Handle<SqlException>()
        .WaitAndRetry(new TimeSpan[]
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(6),
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(24)
        });

    retry.Execute(() =>
    {
        using var scope = app.ApplicationServices.CreateScope();
        scope.ServiceProvider.GetRequiredService<CatalogContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<IdentityDataContext>().Database.Migrate();
    });
}

var app = builder.Build();

// Warm the Redis connection at startup rather than letting it lazily connect on first use.
// StackExchange.Redis.ConnectionMultiplexer is meant to be a long-lived singleton reused for the
// process lifetime — connecting once here, while the app isn't yet serving traffic, avoids the
// connect timing out under load if the very first resolution happened during a request spike
// (observed during load testing: a timed-out first connect surfaced as request failures and
// contributed to CPU/thread-pool pressure severe enough to fail the liveness probe). Failure here
// is non-fatal — the readiness probe already gates traffic on Redis health, and a later request
// will retry the connection.
try
{
    app.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not warm the Redis connection at startup; will retry on first use.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsIntegration())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

ExecuteMigrations(app, config);

app.UseHttpsRedirection();

//app.UseOpenApi();

//app.UseSwaggerUi();

app.UseAuthentication();
app.UseAuthorization();

app.UseResponseCaching();

// --- Health-check endpoints (publicly accessible, no auth required) ---
//
// GET /health       → full detail — all checks, used by APM / dashboards
// GET /health/live  → liveness    — only 'live' tagged checks (is the process alive?)
// GET /health/ready → readiness   — only 'ready' tagged checks (can it serve traffic?)
app.MapHealthChecks(
    ApiEndpoints.Health.Full,
    HealthCheckResponseWriter.DetailedOptions());

app.MapHealthChecks(
    ApiEndpoints.Health.Liveness,
    HealthCheckResponseWriter.DetailedOptions("live"));

app.MapHealthChecks(
    ApiEndpoints.Health.Readiness,
    HealthCheckResponseWriter.DetailedOptions("ready"));

app.UseMiddleware<ResponseTimeMiddlewareAsync>();

app.UseMiddleware<ValidationMappingMiddleware>();

app.MapControllers();

app.Run();
