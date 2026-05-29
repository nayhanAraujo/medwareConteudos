using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.ApiPublica;

public class ApiPartnerJwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiPartnerOptions _options;

  private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/apiconteudos/v1/health",
        "/apiconteudos/v1/token"
    };

    public ApiPartnerJwtMiddleware(RequestDelegate next, IOptions<ApiPartnerOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/apiconteudos/v1", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (HttpMethods.IsOptions(context.Request.Method) || PublicPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        var error = ValidateJwt(context);
        if (error != null)
        {
            context.Response.StatusCode = error.Value.Status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(error.Value.Body));
            return;
        }

        await _next(context);
    }

    private (int Status, object Body)? ValidateJwt(HttpContext context)
    {
        var auth = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(auth))
            return (401, new { success = false, error = "Não autorizado", message = "Header Authorization com Bearer JWT é obrigatório" });

        var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim() : auth.Trim();
        if (string.IsNullOrEmpty(token))
            return (401, new { success = false, error = "Não autorizado", message = "Token JWT não informado" });

        if (string.IsNullOrEmpty(_options.JwtPassword))
            return (503, new { success = false, error = "Configuração do servidor", message = "Autenticação da API não configurada" });

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(ApiJwtKeyHelper.GetKeyBytes(_options.JwtSecret));
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = false
            }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            var senha = jwt.Claims.FirstOrDefault(c => c.Type == "senha" || c.Type == "password")?.Value;
            var datahoraStr = jwt.Claims.FirstOrDefault(c => c.Type == "datahora" || c.Type == "datetime")?.Value;

            if (senha != _options.JwtPassword)
                return (401, new { success = false, error = "Não autorizado", message = "Senha no token não confere" });

            if (string.IsNullOrEmpty(datahoraStr))
                return (401, new { success = false, error = "Token inválido", message = "Campo datahora (ou datetime) é obrigatório no payload do JWT" });

            if (!DateTime.TryParse(datahoraStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var datahora))
                return (401, new { success = false, error = "Token inválido", message = "Data/hora no token em formato inválido" });

            if (datahora.Kind == DateTimeKind.Utc) datahora = datahora.ToUniversalTime();
            else datahora = datahora.ToUniversalTime();

            var now = DateTime.UtcNow;
            if (datahora > now)
                return (401, new { success = false, error = "Token inválido", message = "Data/hora do token não pode ser no futuro" });

            var diffHours = (now - datahora).TotalHours;
            if (diffHours > _options.JwtDatetimeToleranceHours)
                return (401, new { success = false, error = "Token expirado", message = $"Data/hora do token fora do intervalo permitido (máx. {_options.JwtDatetimeToleranceHours}h)" });

            if (datahora.Date != now.Date)
                return (401, new { success = false, error = "Token inválido", message = "Data/hora do token deve ser do dia atual" });
        }
        catch
        {
            return (401, new { success = false, error = "Token inválido", message = "JWT malformado ou assinatura inválida" });
        }

        return null;
    }
}
