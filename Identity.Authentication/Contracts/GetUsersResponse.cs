namespace Identity.Authentication.Contracts
{
    public record GetUsersResponse(IReadOnlyList<UserResponse> Users, int TotalCount);
}
