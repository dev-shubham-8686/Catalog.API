using Catalog.API.Extensions;
using Catalog.Domian.Extensions;
using Catalog.Domian.Repositories;
using Catalog.Domian.Services;
using Catalog.Infrastructure.Repositories;
using Catalog.InfrastructureSP;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCatalogContext(config.GetSection("DataSource:ConnectionString").Value!);

builder.Services.AddSqlConnectionFactory(config.GetSection("DataSource:ConnectionString").Value!);

builder.Services.AddServices();

builder.Services.AddScoped<IItemRepository, ItemRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Integration"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
