namespace Identity.Authentication
{
    /// <summary>
    /// Authorization policy names shared by every service that trusts tokens issued via this
    /// library (e.g. Catalog.API, Order.Api) — defined once here rather than duplicated per
    /// service, so two services' notion of "Admin" can never silently drift apart.
    /// </summary>
    public static class AuthPolicyNames
    {
        public const string AdminOnly = "Admin";
        public const string TrustedMember = "Trusted";
    }
}
