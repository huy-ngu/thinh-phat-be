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

namespace ThinhPhat.Api.Features.Auth.Login;

// 1. Request & Response DTOs
public sealed record LoginRequest(string Email, string Password);

public sealed record AuthUserDto(Guid Id, string Email, string FullName, string Role);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    DateTime RefreshTokenExpiresAtUtc,
    AuthUserDto User
);

// 2. Request Validator
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

// 3. Endpoint Slice & Handler
public static class LoginEndpoint
{
    public static RouteGroupBuilder MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithSummary("Authenticates user and returns JWT access + refresh tokens")
            .WithRequestValidation<LoginRequest>()
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        LoginRequest request,
        HttpContext httpContext,
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. AsNoTracking query with Role projection
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Invalid email or password."
            );
        }

        var roleName = user.Role?.Name ?? RoleConstants.User;

        // 2. Generate Access Token & Cryptographic Refresh Token
        var accessToken = jwtProvider.GenerateAccessToken(user, roleName);
        var (refreshTokenString, refreshExpiresAt) = jwtProvider.GenerateRefreshToken();

        // 3. Store Refresh Token in DB
        var refreshToken = new Data.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenString,
            ExpiresAtUtc = refreshExpiresAt,
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 4. Set HttpOnly Cookie (Chuẩn Production bảo mật chống XSS)
        AuthCookieHelper.AppendRefreshTokenCookie(
            httpContext.Response,
            refreshTokenString,
            refreshExpiresAt,
            httpContext.Request.IsHttps
        );

        // 5. Response Mapping (Trả token trong body hỗ trợ cả Web và Mobile/API Clients)
        var response = new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: refreshTokenString,
            ExpiresIn: 15 * 60, // 15 minutes in seconds
            RefreshTokenExpiresAtUtc: refreshExpiresAt,
            User: new AuthUserDto(user.Id, user.Email, user.FullName, roleName)
        );

        return TypedResults.Ok(response);
    }
}
