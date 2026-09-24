using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Customers.CreateCustomer;

namespace ThinhPhat.Api.Features.Customers.GetCustomers;

public sealed record GetCustomersResponse(
    List<CustomerDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public static class GetCustomersEndpoint
{
    public static RouteGroupBuilder MapGetCustomers(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetCustomers")
            .WithSummary("Retrieves a paginated list of customers")
            .Produces<GetCustomersResponse>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        AppDbContext db,
        string? search,
        bool? hasOrdered,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Enforce safe pagination bounds to protect 512MB RAM
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => pageSize
        };

        var query = db.Customers.AsNoTracking();

        if (hasOrdered.HasValue)
        {
            query = query.Where(c => c.HasOrdered == hasOrdered.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchKeyword = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(searchKeyword) || c.PhoneNumber.Contains(searchKeyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Direct Linq Projection to DTO (Zero change-tracking allocations)
        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.PhoneNumber,
                c.HasOrdered,
                c.CreatedAtUtc,
                c.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        var response = new GetCustomersResponse(items, totalCount, page, pageSize);
        return TypedResults.Ok(response);
    }
}
