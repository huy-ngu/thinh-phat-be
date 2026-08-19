using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Shared.Services.Cloudinary;

namespace ThinhPhat.Api.Features.Products.DeleteProduct;

public static class DeleteProductEndpoint
{
    public static RouteGroupBuilder MapDeleteProduct(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", HandleAsync)
            .WithName("DeleteProduct")
            .WithSummary("Deletes a product and cleans up its Cloudinary image (Admin only)")
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        ICloudinaryService cloudinaryService,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Product with ID '{id}' was not found."
            );
        }

        // 1. Delete image from Cloudinary if it was stored there
        if (!string.IsNullOrWhiteSpace(product.ImagePublicId))
        {
            await cloudinaryService.DeleteImageAsync(product.ImagePublicId, cancellationToken);
        }

        // 2. Remove product from Database
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
