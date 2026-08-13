using Identity.Authentication.Configurations;
using Identity.Authentication.Data;
using Identity.Authentication.Entities;
using Identity.Authentication.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Authentication.Extensions
{
    public static class IdentityAuthenticationExtensions
    {
        public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetSection("DataSource:ConnectionString").Value!;

            services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

            services.AddDbContext<IdentityDataContext>(opt =>
                opt.UseSqlServer(
                    connectionString,
                    x => x.MigrationsAssembly(typeof(IdentityDataContext).Assembly.GetName().Name)
                        .MigrationsHistoryTable("__EFMigrationsHistory_Identity")));

            services
                .AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<IdentityDataContext>()
                .AddDefaultTokenProviders()
                .AddSignInManager();

            services.AddScoped<ITokenService, TokenService>();

            return services;
        }
    }
}
