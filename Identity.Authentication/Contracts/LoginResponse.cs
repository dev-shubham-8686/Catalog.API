namespace Identity.Authentication.Contracts
{
    public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc);
}
