using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Order.Infrastructure.Extensions
{
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Migrations deliberately live in Order.Infrastructure's own assembly (the EF Core
        /// default) rather than in Order.Api or Order.Worker, since OrderDbContext is shared by
        /// both processes and neither should own the other's migration history.
        /// </summary>
        public static IServiceCollection AddOrderContext(this IServiceCollection services, string connectionString)
        {
            return services.AddDbContext<OrderDbContext>(opt => opt.UseSqlServer(connectionString));
        }
    }
}
