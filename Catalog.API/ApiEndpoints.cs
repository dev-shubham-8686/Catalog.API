namespace Catalog.API
{
    public static class ApiEndpoints
    {
        private const string ApiBase = "api";

        public static class Items
        {
            private const string Base = $"{ApiBase}/items";

            public const string Create = Base;
            public const string Get = $"{Base}/{{id:guid}}";
            public const string GetAll = Base;
            public const string Update = $"{Base}/{{id:guid}}";
            public const string Delete = $"{Base}/{{id:guid}}";
            //public const string GetActors = $"{Base}/{{id:guid}}/actors";
            //public const string AddActor = $"{Base}/{{id:guid}}/actors";
            //public const string RemoveActor = $"{Base}/{{id:guid}}/actors/{{actorId:guid}}";
            //public const string GetRatings = $"{Base}/{{id:guid}}/ratings";
            //public const string AddRating = $"{Base}/{{id:guid}}/ratings";
            //public const string DeleteRating = $"{Base}/{{id:guid}}/ratings";
        }

        /// <summary>
        /// Health-check route constants.
        ///
        /// /health       — All checks with full detail (monitoring dashboards, APM).
        /// /health/live  — Liveness probe  (Kubernetes, load balancer restart decision).
        /// /health/ready — Readiness probe (Kubernetes, load balancer traffic routing).
        /// </summary>
        public static class Health
        {
            public const string Full     = "health";
            public const string Liveness = "health/live";
            public const string Readiness = "health/ready";
        }
    }
}
