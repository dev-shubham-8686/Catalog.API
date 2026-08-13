namespace Identity.Authentication.Services
{
    public record TokenResult(string AccessToken, DateTime ExpiresAtUtc);
}
