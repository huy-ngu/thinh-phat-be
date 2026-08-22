using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Warehouses.CreateWarehouse;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Warehouses.UpdateWarehouse;

public sealed record UpdateWarehouseRequest(
    string Name,
    string Address,
    string PhoneNumber
);

public sealed class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Warehouse name is required.")
            .MaximumLength(200).WithMessage("Warehouse name must not exceed 200 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Invalid Vietnamese phone number format (e.g. 0912345678 or +84912345678).");
    }
}

public static class UpdateWarehouseEndpoint
{
    public static RouteGroupBuilder MapUpdateWarehouse(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync)
            .WithName("UpdateWarehouse")
            .WithSummary("Updates an existing warehouse (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<UpdateWarehouseRequest>()
            .Produces<WarehouseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        UpdateWarehouseRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Warehouse with ID '{id}' was not found."
            );
        }

        var trimmedName = request.Name.Trim();
        var normalizedPhone = request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");

        var nameExists = await db.Warehouses
            .AsNoTracking()
            .AnyAsync(w => w.Id != id && w.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (nameExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: $"Another warehouse with name '{request.Name}' already exists."
            );
        }

        warehouse.Name = trimmedName;
        warehouse.Address = request.Address.Trim();
        warehouse.PhoneNumber = normalizedPhone;
        warehouse.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var response = new WarehouseDto(
            Id: warehouse.Id,
            Name: warehouse.Name,
            Address: warehouse.Address,
            PhoneNumber: warehouse.PhoneNumber,
            CreatedAtUtc: warehouse.CreatedAtUtc,
            UpdatedAtUtc: warehouse.UpdatedAtUtc
        );

        return TypedResults.Ok(response);
    }
}
