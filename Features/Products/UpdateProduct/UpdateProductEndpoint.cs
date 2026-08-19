using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Products.CreateProduct;
using ThinhPhat.Api.Shared.Services.Cloudinary;

namespace ThinhPhat.Api.Features.Products.UpdateProduct;

// 1. DTO
public sealed record UpdateProductRequest(
    string Name,
    decimal Price,
    IFormFile? Image
);

// 2. Request Validator
public sealed class UpdateProductValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}

// 3. Endpoint Slice & Handler
public static class UpdateProductEndpoint
{
    public static RouteGroupBuilder MapUpdateProduct(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync)
            .WithName("UpdateProduct")
            .WithSummary("Updates product info and optionally replaces Cloudinary image (Admin only)")
            .RequireAuthorization("AdminOnly")
            .DisableAntiforgery()
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        [FromForm] string name,
        [FromForm] decimal price,
        IFormFile? image,
        AppDbContext db,
        ICloudinaryService cloudinaryService,
        IValidator<UpdateProductRequest> validator,
        CancellationToken cancellationToken)
    {
        var request = new UpdateProductRequest(name, price, image);
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Product with ID '{id}' was not found."
            );
        }

        // Nếu người dùng chọn tải lên ảnh mới:
        if (image is not null && image.Length > 0)
        {
            var oldPublicId = product.ImagePublicId;

            // 1. Upload ảnh mới lên Cloudinary
            var uploadResult = await cloudinaryService.UploadImageAsync(
                image,
                customFolder: "thinhphat/products",
                cancellationToken: cancellationToken
            );

            product.ImageUrl = uploadResult.Url;
            product.ImagePublicId = uploadResult.PublicId;

            // 2. Dọn dẹp ảnh cũ trên Cloudinary
            if (!string.IsNullOrWhiteSpace(oldPublicId))
            {
                _ = cloudinaryService.DeleteImageAsync(oldPublicId, cancellationToken);
            }
        }

        product.Name = name.Trim();
        product.Price = price;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var response = new ProductDto(
            Id: product.Id,
            Name: product.Name,
            Price: product.Price,
            ImageUrl: product.ImageUrl,
            CreatedAtUtc: product.CreatedAtUtc,
            UpdatedAtUtc: product.UpdatedAtUtc
        );

        return TypedResults.Ok(response);
    }
}
