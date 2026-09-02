namespace EventBus
{
    public class RabbitMqSettings
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string User { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string ExchangeName { get; set; } = "integration_events";
        public string ServiceName { get; set; } = "service";
        public int DispatchIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 50;
        public int PrefetchCount { get; set; } = 20;
        public int MaxRetryCount { get; set; } = 5;
    }
}
