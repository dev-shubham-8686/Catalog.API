using Microsoft.Extensions.DependencyInjection;

namespace Identity.Authentication.Extensions
{
    public static class AuthorizationPolicyExtensions
    {
        /// <summary>
        /// Registers the standard authorization policies every service consuming this library's
        /// tokens should share: <see cref="AuthPolicyNames.AdminOnly"/> (must carry the admin
        /// claim) and <see cref="AuthPolicyNames.TrustedMember"/> (admin OR trusted-member claim).
        /// Call this instead of hand-rolling an equivalent <c>AddAuthorization(...)</c> block per
        /// service, so the policies can't drift between services.
        /// </summary>
        public static IServiceCollection AddStandardAuthorizationPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(x =>
            {
                x.AddPolicy(AuthPolicyNames.AdminOnly,
                    p => p.RequireClaim(AuthClaimNames.Admin, "true"));

                x.AddPolicy(AuthPolicyNames.TrustedMember,
                    p => p.RequireAssertion(c =>
                        c.User.HasClaim(m => m is { Type: AuthClaimNames.Admin, Value: "true" }) ||
                        c.User.HasClaim(m => m is { Type: AuthClaimNames.TrustedMember, Value: "true" })));
            });

            return services;
        }
    }
}
