using Catalog.Infrastructure;
using Catalog.InfrastructureSP;
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


        public static IServiceCollection AddSqlConnectionFactory(this IServiceCollection services, string connectionString)
        {
            MssqlConnectionFactory mssqlConnectionFactory(IServiceProvider _)
            {
                return new MssqlConnectionFactory(connectionString);
            }

            return services.AddSingleton<IDbConnectionFactory>(mssqlConnectionFactory);

        }
    }
}
