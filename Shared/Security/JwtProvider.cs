using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ThinhPhat.Api.Data.Entities;

namespace ThinhPhat.Api.Shared.Security;

public sealed class JwtProvider : IJwtProvider
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _securityKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtProvider(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.SecretKey) || _options.SecretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT SecretKey must be at least 256 bits (32 characters) long.");
        }

        _securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        _signingCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256);
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public string GenerateAccessToken(User user, string roleName)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(ClaimTypes.Role, roleName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes),
            SigningCredentials = _signingCredentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var token = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('='); // URL-safe base64 string

        var expiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);
        return (token, expiresAtUtc);
    }
}
