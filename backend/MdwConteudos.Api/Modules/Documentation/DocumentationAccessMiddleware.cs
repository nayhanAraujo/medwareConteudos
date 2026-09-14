using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace MdwConteudos.Api.Modules.Documentation;

/// <summary>Protects the actual OpenAPI documents, including direct requests outside the portal.</summary>
public sealed class DocumentationAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
        if (path.StartsWith("/swagger/api-v1/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger/web/", StringComparison.OrdinalIgnoreCase))
        {
            var result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { success = false, error = "Autenticação administrativa necessária" });
                return;
            }
            if (!result.Principal!.IsInRole("admin"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { success = false, error = "Acesso exclusivo para administradores" });
                return;
            }
            context.Response.Headers.CacheControl = "no-store";
        }
        await next(context);
    }
}
