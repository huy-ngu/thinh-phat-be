using Microsoft.AspNetCore.Http;

namespace ThinhPhat.Api.Shared.Security;

public static class AuthCookieHelper
{
    public const string RefreshTokenCookieName = "refreshToken";
    public const string AuthCookiePath = "/api/v1/auth";

    /// <summary>
    /// Ghi Refresh Token vào HttpOnly Cookie bảo mật chuẩn Production
    /// </summary>
    public static void AppendRefreshTokenCookie(
        HttpResponse response,
        string refreshToken,
        DateTime expiresAtUtc,
        bool isHttps)
    {
        response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,                                     // Chống XSS (JS không thể đọc cookie)
            Secure = isHttps,                                    // True trên HTTPS, False trên Localhost HTTP
            SameSite = SameSiteMode.Lax,                         // Hỗ trợ cả SPA và subdomains
            Path = AuthCookiePath,                               // Chỉ gửi kèm cookie cho các endpoint /api/v1/auth
            Expires = new DateTimeOffset(expiresAtUtc)
        });
    }

    /// <summary>
    /// Xóa Refresh Token Cookie khỏi trình duyệt client
    /// </summary>
    public static void DeleteRefreshTokenCookie(
        HttpResponse response,
        bool isHttps)
    {
        response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = AuthCookiePath
        });
    }
}
