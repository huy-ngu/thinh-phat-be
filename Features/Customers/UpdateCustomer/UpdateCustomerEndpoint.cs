using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Customers.CreateCustomer;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Customers.UpdateCustomer;

// 1. DTO
public sealed record UpdateCustomerRequest(
    string Name,
    string PhoneNumber
);

// 2. Validator
public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerValidator()
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
public static class UpdateCustomerEndpoint
{
    public static RouteGroupBuilder MapUpdateCustomer(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync)
            .WithName("UpdateCustomer")
            .WithSummary("Updates customer information")
            .WithRequestValidation<UpdateCustomerRequest>()
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        UpdateCustomerRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Customer with ID '{id}' was not found."
            );
        }

        var normalizedPhone = request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");

        // Check if new phone number conflicts with another existing customer
        if (!string.Equals(customer.PhoneNumber, normalizedPhone, StringComparison.OrdinalIgnoreCase))
        {
            var phoneExists = await db.Customers
                .AsNoTracking()
                .AnyAsync(c => c.PhoneNumber == normalizedPhone && c.Id != id, cancellationToken);

            if (phoneExists)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: $"Another customer already uses phone number '{request.PhoneNumber}'."
                );
            }
        }

        var now = DateTime.UtcNow;
        customer.Name = request.Name.Trim();
        customer.PhoneNumber = normalizedPhone;
        customer.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        var response = new CustomerDto(
            Id: customer.Id,
            Name: customer.Name,
            PhoneNumber: customer.PhoneNumber,
            CreatedAtUtc: customer.CreatedAtUtc,
            UpdatedAtUtc: customer.UpdatedAtUtc
        );

        return TypedResults.Ok(response);
    }
}
