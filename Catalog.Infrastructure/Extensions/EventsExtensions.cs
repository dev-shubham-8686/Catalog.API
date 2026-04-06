using Catalog.Domain.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Microsoft.Extensions.Options; // Add this using directive

namespace Catalog.Infrastructure.Extensions
{
    public static class EventsExtensions
    {
        public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
        {
            var config = new EventBusSettings();
            configuration.Bind("EventBus", config);
            services.AddSingleton(config);

            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = config.HostName,
                UserName = config.User,
                Password = config.Password
            };

            services.AddSingleton(factory);

            return services;
        }
    }
}
