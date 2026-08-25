using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Products.CreateProduct;

namespace ThinhPhat.Api.Features.Products.GetProducts;

public sealed record GetProductsResponse(
    List<ProductDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public static class GetProductsEndpoint
{
    public static RouteGroupBuilder MapGetProducts(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetProducts")
            .WithSummary("Retrieves a paginated list of products")
            .Produces<GetProductsResponse>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        AppDbContext db,
        string? search,
        bool includeDeleted = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => pageSize
        };

        var query = db.Products.AsNoTracking();

        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchKeyword = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchKeyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Direct Linq Projection to DTO (Zero change-tracking allocations)
        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Price,
                p.CostPrice,
                p.ImageUrl,
                p.IsDeleted,
                p.CreatedAtUtc,
                p.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var response = new GetProductsResponse(items, totalCount, page, pageSize);
        return TypedResults.Ok(response);
    }
}
