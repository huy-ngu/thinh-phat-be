using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Constants;
using ThinhPhat.Api.Shared.Filters;
using ThinhPhat.Api.Shared.Security;

namespace ThinhPhat.Api.Features.Auth.RefreshToken;

// 1. DTOs
public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    DateTime RefreshTokenExpiresAtUtc
);

// 2. Validator
public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

// 3. Endpoint Slice & Handler (Enterprise Token Rotation & Compromise Detection)
public static class RefreshTokenEndpoint
{
    public static RouteGroupBuilder MapRefreshToken(this RouteGroupBuilder group)
    {
        group.MapPost("/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithSummary("Rotates and issues a new JWT access and refresh token pair")
            .WithRequestValidation<RefreshTokenRequest>()
            .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        RefreshTokenRequest request,
        AppDbContext db,
        IJwtProvider jwtProvider,
        CancellationToken cancellationToken)
    {
        var existingToken = await db.RefreshTokens
            .Include(rt => rt.User)
            .ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (existingToken is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Invalid refresh token."
            );
        }

        // ==============================================================================
        // SECURITY: TOKEN REUSE DETECTION (Compromised Token Prevention)
        // If an already-revoked token is used again, an attacker likely stole it.
        // Action: Revoke ALL active tokens of this user immediately to protect account.
        // ==============================================================================
        if (existingToken.IsRevoked)
        {
            var userActiveTokens = await db.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var token in userActiveTokens)
            {
                token.RevokedAtUtc = now;
            }

            await db.SaveChangesAsync(cancellationToken);

            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Compromised token detected. All active sessions have been revoked."
            );
        }

        if (existingToken.IsExpired)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Refresh token has expired. Please log in again."
            );
        }

        if (existingToken.User is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "User associated with token not found."
            );
        }

        // Rotate Tokens
        var roleName = existingToken.User.Role?.Name ?? RoleConstants.User;
        var newAccessToken = jwtProvider.GenerateAccessToken(existingToken.User, roleName);
        var (newRefreshTokenString, newRefreshExpiresAt) = jwtProvider.GenerateRefreshToken();

        // Mark current token as revoked and track replacement
        existingToken.RevokedAtUtc = DateTime.UtcNow;
        existingToken.ReplacedByToken = newRefreshTokenString;

        // Provision new refresh token
        var newRefreshToken = new Data.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            Token = newRefreshTokenString,
            ExpiresAtUtc = newRefreshExpiresAt,
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var response = new RefreshTokenResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRefreshTokenString,
            ExpiresIn: 15 * 60,
            RefreshTokenExpiresAtUtc: newRefreshExpiresAt
        );

        return TypedResults.Ok(response);
    }
}
