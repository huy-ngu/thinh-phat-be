using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Products.CreateProduct;
using ThinhPhat.Api.Features.Products.DeleteProduct;
using ThinhPhat.Api.Features.Products.GetProductById;
using ThinhPhat.Api.Features.Products.GetProducts;
using ThinhPhat.Api.Features.Products.UpdateProduct;

namespace ThinhPhat.Api.Features.Products;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products")
            .WithTags("Products");

        // Public/Authenticated Read
        group.MapGetProducts();
        group.MapGetProductById();

        // Admin-only Write Operations
        group.MapCreateProduct();
        group.MapUpdateProduct();
        group.MapDeleteProduct();

        return app;
    }
}
