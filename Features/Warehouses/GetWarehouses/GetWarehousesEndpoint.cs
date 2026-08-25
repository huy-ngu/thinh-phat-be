using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Warehouses.CreateWarehouse;

namespace ThinhPhat.Api.Features.Warehouses.GetWarehouses;

public sealed record GetWarehousesResponse(
    List<WarehouseDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public static class GetWarehousesEndpoint
{
    public static RouteGroupBuilder MapGetWarehouses(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetWarehouses")
            .WithSummary("Retrieves a paginated list of warehouses")
            .Produces<GetWarehousesResponse>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        AppDbContext db,
        string? search,
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

        var query = db.Warehouses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchKeyword = search.Trim().ToLower();
            query = query.Where(w => w.Name.ToLower().Contains(searchKeyword) 
                                  || w.Address.ToLower().Contains(searchKeyword) 
                                  || w.Province.ToLower().Contains(searchKeyword) 
                                  || w.District.ToLower().Contains(searchKeyword) 
                                  || (w.Ward != null && w.Ward.ToLower().Contains(searchKeyword)) 
                                  || w.PhoneNumber.Contains(searchKeyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(w => w.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WarehouseDto(
                w.Id,
                w.Name,
                w.Province,
                w.District,
                w.Ward,
                w.Address,
                w.PhoneNumber,
                w.CreatedAtUtc,
                w.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var response = new GetWarehousesResponse(items, totalCount, page, pageSize);
        return TypedResults.Ok(response);
    }
}
