using Identity.Authentication.Entities;

namespace Identity.Authentication.Services
{
    public interface ITokenService
    {
        Task<TokenResult> GenerateTokenAsync(ApplicationUser user, IList<string> roles, CancellationToken cancellationToken = default);
    }
}
