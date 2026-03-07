using Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Extensions
{
    public static class DatabaseExtensions
    {
        public static IServiceCollection AddCatalogContext(this IServiceCollection services, string connectionString)
        {
            return services
                .AddEntityFrameworkSqlServer()
                .AddDbContext<CatalogContext>(opt =>
                {
                    opt.UseSqlServer(
                        connectionString,
                        x => x.MigrationsAssembly(typeof(Program).Assembly.GetName().Name)
                    );
                        
                });
        }
    }
}
