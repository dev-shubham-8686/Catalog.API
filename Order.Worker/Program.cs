using Catalog.Contracts.Events;
using EventBus.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Order.Domain.Repositories;
using Order.Infrastructure;
using Order.Infrastructure.Extensions;
using Order.Infrastructure.Repositories;
using Order.Worker.Handlers;
using Order.Worker.Sagas;
using Polly;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

builder.Services.AddOrderContext(config.GetSection("DataSource:ConnectionString").Value!);
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.Configure<PaymentSimulationSettings>(config.GetSection("PaymentSimulation"));
builder.Services.AddScoped<IOrderSagaOrchestrator, OrderSagaOrchestrator>();

// Producer side: the orchestrator publishes ReleaseStockIntegrationEvent (compensation) via the
// transactional outbox when a reserved order later fails its simulated payment step.
builder.Services.AddEventOutbox<OrderDbContext>(config);

// Consumer side: reacts to Catalog.Worker's replies to OrderPlacedIntegrationEvent.
builder.Services.AddEventConsumer<OrderDbContext>(config, subscriptions => subscriptions
    .Subscribe<StockReservedIntegrationEvent, StockReservedEventHandler>()
    .Subscribe<StockReservationFailedIntegrationEvent, StockReservationFailedEventHandler>());

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

ExecuteMigrations(app, config);

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.Run();
