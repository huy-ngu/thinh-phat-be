using ThinhPhat.Api.Data.Entities;

namespace ThinhPhat.Api.Shared.Security;

public interface IJwtProvider
{
    string GenerateAccessToken(User user, string roleName);
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken();
}
