namespace Identity.Authentication.Endpoints
{
    public static class AuthEndpoints
    {
        private const string ApiBase = "api";

        public static class Auth
        {
            private const string Base = $"{ApiBase}/auth";

            public const string Register = $"{Base}/register";
            public const string Login = $"{Base}/login";
            public const string Users = $"{Base}/users";
        }
    }
}
