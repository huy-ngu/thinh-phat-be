using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Products.CreateProduct;

namespace ThinhPhat.Api.Features.Products.GetProductById;

public static class GetProductByIdEndpoint
{
    public static RouteGroupBuilder MapGetProductById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetProductById")
            .WithSummary("Retrieves product details by ID")
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Price,
                p.ImageUrl,
                p.CreatedAtUtc,
                p.UpdatedAtUtc
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Product with ID '{id}' was not found."
            );
        }

        return TypedResults.Ok(product);
    }
}
