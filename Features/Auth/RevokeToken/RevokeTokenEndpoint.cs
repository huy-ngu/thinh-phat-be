using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Auth.RevokeToken;

public sealed record RevokeTokenRequest(string RefreshToken);

public sealed class RevokeTokenValidator : AbstractValidator<RevokeTokenRequest>
{
    public RevokeTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public static class RevokeTokenEndpoint
{
    public static RouteGroupBuilder MapRevokeToken(this RouteGroupBuilder group)
    {
        group.MapPost("/revoke", HandleAsync)
            .WithName("RevokeToken")
            .WithSummary("Revokes an active refresh token (Logout)")
            .WithRequestValidation<RevokeTokenRequest>()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        RevokeTokenRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (token is null || token.IsRevoked)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: "Active refresh token not found."
            );
        }

        token.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new { message = "Token revoked successfully." });
    }
}
