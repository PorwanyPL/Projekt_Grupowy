using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SocialContent.Api.Swagger;

/// <summary>
/// Oznacza w Swaggerze (kłódka i przycisk Authorize) tylko te operacje, które mają [Authorize],
/// zamiast wymagać tokenu globalnie.
/// </summary>
public class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controllerAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? Array.Empty<object>();
        var methodAttributes = context.MethodInfo.GetCustomAttributes(true);
        var all = controllerAttributes.Concat(methodAttributes).ToList();

        var requiresAuth = all.OfType<AuthorizeAttribute>().Any();
        var allowsAnonymous = all.OfType<AllowAnonymousAttribute>().Any();
        if (!requiresAuth || allowsAnonymous) return;

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            }
        };
    }
}
