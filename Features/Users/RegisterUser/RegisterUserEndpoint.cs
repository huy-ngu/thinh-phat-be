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

namespace ThinhPhat.Api.Features.Users.RegisterUser;

// 1. Request DTO
public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FullName
);

// 2. Response DTO
public sealed record RegisterUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAtUtc
);

// 3. Request Validator (FluentValidation)
public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("FullName is required.")
            .MaximumLength(120).WithMessage("FullName must not exceed 120 characters.");
    }
}

// 4. Endpoint Registration & Handler (Vertical Slice)
public static class RegisterUserEndpoint
{
    public static RouteGroupBuilder MapRegisterUser(this RouteGroupBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .WithName("RegisterUser")
            .WithSummary("Registers a new user account")
            .WithDescription("Creates a new user account with a hashed password and assigns default User role.")
            .WithRequestValidation<RegisterUserRequest>()
            .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        RegisterUserRequest request,
        AppDbContext db,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Zero-allocation read check (AsNoTracking projection to avoid EF Core Change Tracker overhead)
        var emailExists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: $"User with email '{request.Email}' already exists."
            );
        }

        // 2. Hash password statelessly
        var passwordHash = passwordHasher.Hash(request.Password);

        // 3. Manual Mapping to Entities (Zero dynamic reflection / No AutoMapper)
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = request.FullName.Trim(),
            RoleId = RoleConstants.UserRoleId,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 4. Database Persistence
        await db.Users.AddAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 5. Manual Mapping from Entity to Response DTO
        var response = new RegisterUserResponse(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: RoleConstants.User,
            CreatedAtUtc: user.CreatedAtUtc
        );

        return TypedResults.Created($"/api/v1/users/{user.Id}", response);
    }
}
