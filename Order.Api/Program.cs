using Catalog.Client.Extensions;
using EventBus.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Order.Api;
using Order.Domain.Repositories;
using Order.Domain.Services;
using Order.Infrastructure;
using Order.Infrastructure.Extensions;
using Order.Infrastructure.Repositories;
using Polly;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOrderContext(config.GetSection("DataSource:ConnectionString").Value!);
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddCatalogClient(config.GetSection("CatalogApi:BaseUrl").Value!);

// Producer side only — Order.Api creates orders and enqueues OrderPlacedIntegrationEvent via the
// transactional outbox. It does not consume events itself (that's Order.Worker's job).
builder.Services.AddEventOutbox<OrderDbContext>(config);

builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddSqlServer(config.GetSection("DataSource:ConnectionString").Value!, name: "orders-sqlserver", tags: ["ready"]);

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
        scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.Migrate();
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Integration")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

ExecuteMigrations(app, config);

app.MapHealthChecks(ApiEndpoints.Health.Liveness, new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks(ApiEndpoints.Health.Readiness, new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();
