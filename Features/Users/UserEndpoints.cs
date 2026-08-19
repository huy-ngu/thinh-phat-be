using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Users.RegisterUser;

namespace ThinhPhat.Api.Features.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users");

        // Map individual feature slices
        group.MapRegisterUser();

        return app;
    }
}
