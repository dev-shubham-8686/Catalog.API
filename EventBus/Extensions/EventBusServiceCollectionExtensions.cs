using EventBus.Consuming;
using EventBus.Idempotency;
using EventBus.Outbox;
using EventBus.Publishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EventBus.Extensions
{
    public static class EventBusServiceCollectionExtensions
    {
        /// <summary>
        /// Producer side: registers the transactional outbox (IOutbox, scoped to TContext) and the
        /// background dispatcher that publishes queued messages to RabbitMQ.
        /// </summary>
        public static IServiceCollection AddEventOutbox<TContext>(this IServiceCollection services, IConfiguration configuration, string sectionName = "EventBus")
            where TContext : DbContext
        {
            services.Configure<RabbitMqSettings>(configuration.GetSection(sectionName));

            services.AddScoped<IOutbox, EfOutbox<TContext>>();
            services.AddSingleton<IEventBus, RabbitMqEventBus>();
            services.AddHostedService<OutboxProcessorHostedService<TContext>>();

            // Convenience registration for consumers that just need broker connectivity
            // (e.g. a readiness health check) without the full publish/dispatch pipeline.
            services.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
                return new ConnectionFactory
                {
                    HostName = settings.HostName,
                    UserName = settings.User,
                    Password = settings.Password
                };
            });

            return services;
        }

        /// <summary>
        /// Consumer side: registers the RabbitMQ consumer host, the event/handler subscription map,
        /// and an idempotency store scoped to TContext so at-least-once delivery is safe to handle.
        /// </summary>
        public static IServiceCollection AddEventConsumer<TContext>(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<EventBusSubscriptionManager> subscribe,
            string sectionName = "EventBus")
            where TContext : DbContext
        {
            services.Configure<RabbitMqSettings>(configuration.GetSection(sectionName));

            var subscriptions = new EventBusSubscriptionManager();
            subscribe(subscriptions);
            services.AddSingleton(subscriptions);

            foreach (var subscription in subscriptions.Subscriptions)
            {
                services.AddScoped(subscription.HandlerType);
            }

            services.AddScoped<IIdempotencyStore, EfIdempotencyStore<TContext>>();
            services.AddHostedService<RabbitMqConsumerHostedService>();

            return services;
        }
    }
}
