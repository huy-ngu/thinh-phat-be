using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ThinhPhat.Api.Features.Auth.GetMe;
using ThinhPhat.Api.Features.Auth.Login;
using ThinhPhat.Api.Features.Auth.RefreshToken;
using ThinhPhat.Api.Features.Auth.RevokeToken;

namespace ThinhPhat.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        group.MapLogin();
        group.MapRefreshToken();
        group.MapRevokeToken();
        group.MapGetMe();

        return app;
    }
}
