using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Customers.CreateCustomer;
using ThinhPhat.Api.Features.Customers.DeleteCustomer;
using ThinhPhat.Api.Features.Customers.GetCustomerById;
using ThinhPhat.Api.Features.Customers.GetCustomers;
using ThinhPhat.Api.Features.Customers.UpdateCustomer;

namespace ThinhPhat.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers")
            .WithTags("Customers")
            .RequireAuthorization(); // Bảo vệ toàn bộ CRUD Customer với JWT Bearer

        group.MapCreateCustomer();
        group.MapGetCustomers();
        group.MapGetCustomerById();
        group.MapUpdateCustomer();
        group.MapDeleteCustomer();

        return app;
    }
}
