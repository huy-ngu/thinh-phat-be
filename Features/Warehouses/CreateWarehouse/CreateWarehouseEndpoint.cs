using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Filters;

namespace ThinhPhat.Api.Features.Warehouses.CreateWarehouse;

// 1. DTOs
public sealed record CreateWarehouseRequest(
    string Name,
    string Province,
    string District,
    string? Ward,
    string Address,
    string PhoneNumber
);

public sealed record WarehouseDto(
    Guid Id,
    string Name,
    string Province,
    string District,
    string? Ward,
    string Address,
    string PhoneNumber,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// 2. Validator
public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouseRequest>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Warehouse name is required.")
            .MaximumLength(200).WithMessage("Warehouse name must not exceed 200 characters.");

        RuleFor(x => x.Province)
            .NotEmpty().WithMessage("Province is required.")
            .MaximumLength(100).WithMessage("Province must not exceed 100 characters.");

        RuleFor(x => x.District)
            .NotEmpty().WithMessage("District is required.")
            .MaximumLength(100).WithMessage("District must not exceed 100 characters.");

        RuleFor(x => x.Ward)
            .MaximumLength(100).WithMessage("Ward must not exceed 100 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .Matches(@"^(0|\+84)[0-9]{9,10}$").WithMessage("Invalid Vietnamese phone number format (e.g. 0912345678 or +84912345678).");
    }
}

// 3. Endpoint Slice & Handler
public static class CreateWarehouseEndpoint
{
    public static RouteGroupBuilder MapCreateWarehouse(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateWarehouse")
            .WithSummary("Creates a new warehouse (Admin only)")
            .RequireAuthorization("AdminOnly")
            .WithRequestValidation<CreateWarehouseRequest>()
            .Produces<WarehouseDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        CreateWarehouseRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");
        var trimmedName = request.Name.Trim();

        var nameExists = await db.Warehouses
            .AsNoTracking()
            .AnyAsync(w => w.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (nameExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: $"Warehouse with name '{request.Name}' already exists."
            );
        }

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Province = request.Province.Trim(),
            District = request.District.Trim(),
            Ward = string.IsNullOrWhiteSpace(request.Ward) ? null : request.Ward.Trim(),
            Address = request.Address.Trim(),
            PhoneNumber = normalizedPhone,
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.Warehouses.AddAsync(warehouse, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var response = new WarehouseDto(
            Id: warehouse.Id,
            Name: warehouse.Name,
            Province: warehouse.Province,
            District: warehouse.District,
            Ward: warehouse.Ward,
            Address: warehouse.Address,
            PhoneNumber: warehouse.PhoneNumber,
            CreatedAtUtc: warehouse.CreatedAtUtc,
            UpdatedAtUtc: warehouse.UpdatedAtUtc
        );

        return TypedResults.Created($"/api/v1/warehouses/{warehouse.Id}", response);
    }
}
