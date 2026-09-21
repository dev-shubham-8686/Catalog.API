namespace Identity.Authentication
{
    /// <summary>
    /// Claim type names emitted by <see cref="Services.TokenService"/> and consumed by
    /// <see cref="AuthPolicyNames"/>'s policies — defined once here so every service reads the
    /// exact same claim names the token issuer writes.
    /// </summary>
    public static class AuthClaimNames
    {
        public const string Admin = "admin";
        public const string TrustedMember = "trusted_member";
        public const string UserId = "userid";
    }
}
