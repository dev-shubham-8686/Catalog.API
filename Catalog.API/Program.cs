using Catalog.API;
using Catalog.API.Extensions;
using Catalog.API.HealthChecks;
using Catalog.API.Middleware;
using Catalog.Domain.Extensions;
using Catalog.Domain.Repositories;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Extensions;
using Catalog.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCatalogContext(config.GetSection("DataSource:ConnectionString").Value!);
builder.Services.AddSqlConnectionFactory(config.GetSection("DataSource:ConnectionString").Value!);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.TokenValidationParameters = new TokenValidationParameters
    {
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = config["Jwt:Issuer"],
        ValidAudience = config["Jwt:Audience"],
        ValidateIssuer = true,
        ValidateAudience = true
    };
});

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
        .AddCheck<RedisCacheHealthCheck>("cache_health_check")
        .AddSqlServer(config.GetSection("DataSource:ConnectionString").Value!);

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

app.UseAuthentication();
app.UseAuthorization();

app.UseResponseCaching();

app.UseHealthChecks("/health");

app.UseMiddleware<ResponseTimeMiddlewareAsync>();

app.UseMiddleware<ValidationMappingMiddleware>();

app.MapControllers();

app.Run();
