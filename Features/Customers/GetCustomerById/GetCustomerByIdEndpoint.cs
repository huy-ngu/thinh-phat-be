using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Customers.CreateCustomer;

namespace ThinhPhat.Api.Features.Customers.GetCustomerById;

public static class GetCustomerByIdEndpoint
{
    public static RouteGroupBuilder MapGetCustomerById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetCustomerById")
            .WithSummary("Retrieves customer details by ID")
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // AsNoTracking direct Linq Projection to DTO
        var customer = await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.PhoneNumber,
                c.CreatedAtUtc,
                c.UpdatedAtUtc
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: $"Customer with ID '{id}' was not found."
            );
        }

        return TypedResults.Ok(customer);
    }
}
