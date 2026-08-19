using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;

namespace ThinhPhat.Api.Features.Auth.GetMe;

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAtUtc
);

public static class GetMeEndpoint
{
    public static RouteGroupBuilder MapGetMe(this RouteGroupBuilder group)
    {
        group.MapGet("/me", HandleAsync)
            .WithName("GetMe")
            .WithSummary("Retrieves the currently authenticated user's profile")
            .RequireAuthorization()
            .Produces<UserProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        ClaimsPrincipal userPrincipal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var subClaim = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? userPrincipal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        // AsNoTracking projection straight to DTO for minimum allocations
        var profile = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfileResponse(
                u.Id,
                u.Email,
                u.FullName,
                u.Role != null ? u.Role.Name : "User",
                u.CreatedAtUtc
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(profile);
    }
}
