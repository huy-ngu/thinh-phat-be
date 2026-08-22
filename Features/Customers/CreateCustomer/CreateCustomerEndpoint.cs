using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Customers.CreateCustomer;

// 1. DTOs
public sealed record CreateCustomerRequest(
    string Name,
    string PhoneNumber
);

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// 2. Validator
public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(150).WithMessage("Customer name must not exceed 150 characters.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Invalid Vietnamese phone number format (e.g. 0912345678 or +84912345678).");
    }
}

// 3. Endpoint Slice & Handler
public static class CreateCustomerEndpoint
{
    public static RouteGroupBuilder MapCreateCustomer(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateCustomer")
            .WithSummary("Creates a new customer with a unique phone number (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<CreateCustomerRequest>()
            .Produces<CustomerDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        CreateCustomerRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");

        // 1. AsNoTracking Check for Phone Uniqueness
        var phoneExists = await db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.PhoneNumber == normalizedPhone, cancellationToken);

        if (phoneExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: $"Customer with phone number '{request.PhoneNumber}' already exists."
            );
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            PhoneNumber = normalizedPhone,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 2. Database Persistence
        await db.Customers.AddAsync(customer, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 3. Manual Response Mapping
        var response = new CustomerDto(
            Id: customer.Id,
            Name: customer.Name,
            PhoneNumber: customer.PhoneNumber,
            CreatedAtUtc: customer.CreatedAtUtc,
            UpdatedAtUtc: customer.UpdatedAtUtc
        );

        return TypedResults.Created($"/api/v1/customers/{customer.Id}", response);
    }
}
