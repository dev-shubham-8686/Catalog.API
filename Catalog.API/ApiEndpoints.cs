namespace Catalog.API
{
    public static class ApiEndpoints
    {
        private const string ApiBase = "api";

        public static class Movies
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
    }
}
