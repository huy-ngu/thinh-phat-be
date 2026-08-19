namespace ThinhPhat.Api.Shared.Constants;

public static class RoleConstants
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly Guid AdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
}
