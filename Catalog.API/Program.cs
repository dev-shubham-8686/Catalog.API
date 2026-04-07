using Catalog.API.Extensions;
using Catalog.API.Middleware;
using Catalog.Domain.Extensions;
using Catalog.Domain.Repositories;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Extensions;
using Catalog.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCatalogContext(config.GetSection("DataSource:ConnectionString").Value!);
builder.Services.AddSqlConnectionFactory(config.GetSection("DataSource:ConnectionString").Value!);

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


void ExecuteMigrations(IApplicationBuilder app,
         IWebHostEnvironment env)
{
    if (env.IsDevelopment() || env.IsIntegration()) return;

    var retry = Policy.Handle<SqlException>()
        .WaitAndRetry(new TimeSpan[]
        {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(6),
                    TimeSpan.FromSeconds(12)
        });

    retry.Execute(() => app.ApplicationServices.GetService<CatalogContext>()!.Database.Migrate());
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsIntegration())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

ExecuteMigrations(app, app.Environment);

app.UseHttpsRedirection();

//app.UseOpenApi();

//app.UseSwaggerUi();

app.UseAuthorization();

app.UseResponseCaching();

app.UseMiddleware<ResponseTimeMiddlewareAsync>();

app.MapControllers();

app.Run();
