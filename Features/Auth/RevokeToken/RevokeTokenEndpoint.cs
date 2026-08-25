using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Shared.Filters;
using ThinhPhat.Api.Shared.Security;

namespace ThinhPhat.Api.Features.Auth.RevokeToken;

// 1. DTOs
public sealed record RevokeTokenRequest(string? RefreshToken = null);

// 2. Endpoint Slice & Handler
public static class RevokeTokenEndpoint
{
    public static RouteGroupBuilder MapRevokeToken(this RouteGroupBuilder group)
    {
        group.MapPost("/revoke", HandleAsync)
            .WithName("RevokeToken")
            .WithSummary("Revokes an active refresh token and clears HttpOnly cookie (Logout)")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        AppDbContext db,
        RevokeTokenRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var tokenString = httpContext.Request.Cookies[AuthCookieHelper.RefreshTokenCookieName]
                          ?? request?.RefreshToken?.Trim();

        // Luôn xóa cookie khỏi trình duyệt client
        AuthCookieHelper.DeleteRefreshTokenCookie(httpContext.Response, httpContext.Request.IsHttps);

        if (!string.IsNullOrWhiteSpace(tokenString))
        {
            var token = await db.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == tokenString, cancellationToken);

            if (token is not null && !token.IsRevoked)
            {
                token.RevokedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return TypedResults.Ok(new { message = "Token revoked successfully and cookie cleared." });
    }
}
