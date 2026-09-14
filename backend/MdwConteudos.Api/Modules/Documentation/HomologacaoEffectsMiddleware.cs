namespace MdwConteudos.Api.Modules.Documentation;

public sealed class HomologacaoEffectsMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (environment.IsEnvironment("Homologacao"))
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            if (path.StartsWith("/api/conversions") || path.StartsWith("/api/voice")
                || path.Contains("/publicacao") || path.Contains("/firebird")
                || path.Contains("/agente") || path.Contains("email") || path.Contains("enviar")
                || path.Contains("notification") || path.Contains("notificacao")
                || path.Contains("azure") || path.Contains("solicitar-aprovacao")
                || path.Contains("/forgot-password") || path.Contains("/reset-password")
                || path.Contains("/aprovar/"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { success = false, error = "Integração externa indisponível em homologação" });
                return;
            }
        }
        await next(context);
    }
}
