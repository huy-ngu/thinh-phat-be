using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ThinhPhat.Api.Shared.Filters;

public sealed class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var requestObj = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (requestObj is null)
        {
            return TypedResults.Problem(
                title: "Bad Request",
                detail: $"Request payload of type '{typeof(TRequest).Name}' is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (validator is not null)
        {
            var validationResult = await validator.ValidateAsync(requestObj, context.HttpContext.RequestAborted);
            if (!validationResult.IsValid)
            {
                return TypedResults.ValidationProblem(validationResult.ToDictionary());
            }
        }

        return await next(context);
    }
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder) where TRequest : class
    {
        return builder.AddEndpointFilter<ValidationFilter<TRequest>>();
    }
}
