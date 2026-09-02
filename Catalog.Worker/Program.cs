using Catalog.Contracts.Events;
using Catalog.Worker;
using Catalog.Worker.Handlers;
using EventBus.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
var connectionString = config.GetSection("DataSource:ConnectionString").Value!;
var cacheConnectionString = config.GetSection("CacheSettings:ConnectionString").Value!;

builder.Services.AddDbContext<WorkerDbContext>(opt =>
    opt.UseSqlServer(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory_Worker")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(cacheConnectionString));

builder.Services.AddEventConsumer<WorkerDbContext>(config, subscriptions => subscriptions
    .Subscribe<ItemCreatedIntegrationEvent, ItemCreatedEventHandler>()
    .Subscribe<ItemUpdatedIntegrationEvent, ItemUpdatedEventHandler>()
    .Subscribe<ItemDeletedIntegrationEvent, ItemDeletedEventHandler>());

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
        scope.ServiceProvider.GetRequiredService<WorkerDbContext>().Database.Migrate();
    });
}

var app = builder.Build();

ExecuteMigrations(app, config);

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

app.Run();
