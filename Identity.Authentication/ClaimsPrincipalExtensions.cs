using System.Security.Claims;

namespace Identity.Authentication
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Reads the "userid" claim <see cref="Services.TokenService"/> puts on every issued
        /// token. Shared so every service extracts the caller's id the same way instead of each
        /// re-implementing its own claim lookup.
        /// </summary>
        public static Guid? GetUserId(this ClaimsPrincipal principal)
        {
            var userId = principal.Claims.SingleOrDefault(c => c.Type == AuthClaimNames.UserId);

            return Guid.TryParse(userId?.Value, out var parsedId) ? parsedId : null;
        }
    }
}
