namespace Order.Api
{
    public static class ApiEndpoints
    {
        private const string ApiBase = "api";

        public static class Orders
        {
            private const string Base = $"{ApiBase}/orders";

            public const string Create = Base;
            public const string Get = $"{Base}/{{id:guid}}";
            public const string GetAll = Base;
            public const string Mine = $"{Base}/me";
        }

        public static class Health
        {
            public const string Liveness = "health/live";
            public const string Readiness = "health/ready";
        }
    }
}
