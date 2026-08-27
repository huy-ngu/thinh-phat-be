using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Services.Cloudinary;

namespace ThinhPhat.Api.Features.Products.CreateProduct;

// 1. DTOs
public sealed record CreateProductRequest(
    string Name,
    decimal Price,
    decimal CostPrice,
    IFormFile? Image
);

public sealed record ProductDto(
    Guid Id,
    string Name,
    decimal Price,
    decimal CostPrice,
    string ImageUrl,
    bool IsDeleted,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

// 2. Request Validator
public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Cost price must be greater than or equal to zero.");

        RuleFor(x => x.Image)
            .NotNull().WithMessage("Product image file is required.")
            .Must(img => img != null && img.Length > 0).WithMessage("Product image file cannot be empty.");
    }
}

// 3. Endpoint Slice & Handler
public static class CreateProductEndpoint
{
    public static RouteGroupBuilder MapCreateProduct(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateProduct")
            .WithSummary("Creates a new product and uploads image to Cloudinary (Admin only)")
            .RequireAuthorization("AdminOnly")
            .DisableAntiforgery()
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        [FromForm] string name,
        [FromForm] decimal price,
        [FromForm] decimal costPrice,
        IFormFile image,
        AppDbContext db,
        ICloudinaryService cloudinaryService,
        IValidator<CreateProductRequest> validator,
        CancellationToken cancellationToken)
    {
        var request = new CreateProductRequest(name, price, costPrice, image);
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        // 1. Stream image trực tiếp lên Cloudinary (Không buffer mảng byte vào RAM)
        var uploadResult = await cloudinaryService.UploadImageAsync(
            image,
            customFolder: "thinhphat/products",
            cancellationToken: cancellationToken
        );

        // 2. Lưu thông tin sản phẩm và link ảnh trả về từ Cloudinary vào PostgreSQL
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Price = price,
            CostPrice = costPrice,
            ImageUrl = uploadResult.Url,
            ImagePublicId = uploadResult.PublicId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.Products.AddAsync(product, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 3. Response Mapping
        var response = new ProductDto(
            Id: product.Id,
            Name: product.Name,
            Price: product.Price,
            CostPrice: product.CostPrice,
            ImageUrl: product.ImageUrl,
            IsDeleted: product.IsDeleted,
            CreatedAtUtc: product.CreatedAtUtc,
            UpdatedAtUtc: product.UpdatedAtUtc
        );

        return TypedResults.Created($"/api/v1/products/{product.Id}", response);
    }
}
