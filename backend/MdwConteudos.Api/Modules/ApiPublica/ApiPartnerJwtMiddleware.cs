using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;
using System.Security.Claims;

namespace MdwConteudos.Api.Modules.ApiPublica;

public class ApiPartnerJwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiPartnerOptions _options;
    private readonly TimeProvider _time;

    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/apiconteudos/v1/health",
        "/apiconteudos/v1/token",
        "/api/v1/health",
        "/api/v1/token"
    };

    public ApiPartnerJwtMiddleware(RequestDelegate next, IOptions<ApiPartnerOptions> options, TimeProvider? timeProvider = null)
    {
        _next = next;
        _options = options.Value;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = (context.Request.Path.Value ?? "").TrimEnd('/');
        var internalApi = context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase);
        if (!internalApi && !context.Request.Path.StartsWithSegments("/apiconteudos/v1", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (HttpMethods.IsOptions(context.Request.Method) || PublicPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        // Web authentication runs first. Partner payloads never become web identities.
        if (internalApi && context.User.Identity?.IsAuthenticated == true)
        {
            var domain = InternalDomain(path);
            var action = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
                ? PermissionActions.Visualizar : PermissionActions.Editar;
            var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var permissions = context.RequestServices.GetRequiredService<IPermissionService>();
            if (domain is null || !int.TryParse(id, out var userId)
                || !await permissions.HasPermissionAsync(userId, context.User.FindFirstValue(ClaimTypes.Role) ?? "", domain, action, context.RequestAborted))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { success = false, error = "Acesso negado", message = "Usuário sem permissão para esta operação." });
                return;
            }
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

    public static string? InternalDomain(string path)
    {
        var segment = path.Trim('/').Split('/').ElementAtOrDefault(2)?.ToLowerInvariant();
        return segment switch
        {
            "variaveis" or "normalidades" or "normalidades_ecodopplercardiograma" or "ecodoppler" or "clientes" => PermissionDomains.Variaveis,
            "formulas" => PermissionDomains.Formulas,
            "referencias" => PermissionDomains.Referencias,
            "especialidades" => PermissionDomains.Assistente,
            "relatorios" => PermissionDomains.Relatorios,
            "scripts" => PermissionDomains.Scripts,
            "paineis" => PermissionDomains.Paineis,
            "info" or "sistema" => PermissionDomains.Biblioteca,
            _ => null
        };
    }

    private (int Status, object Body)? ValidateJwt(HttpContext context)
    {
        var auth = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(auth))
            return (401, new { success = false, error = "Não autorizado", message = "Header Authorization com Bearer JWT é obrigatório" });

        auth = auth.Trim();
        var token = auth.StartsWith("Bearer ", StringComparison.Ordinal) ? auth[7..].Trim() : auth;
        if (string.IsNullOrEmpty(token))
            return (401, new { success = false, error = "Não autorizado", message = "Token JWT não informado" });

        try
        {
            var now = _time.GetUtcNow();
            var payload = ApiJwtKeyHelper.VerifyToken(token, _options.JwtSecret, _options.CanAcceptLegacyHashedKey(now));
            var unixNow = (now - DateTimeOffset.UnixEpoch).TotalSeconds;
            // PyJWT validates these optional registered claims even when exp is not required.
            foreach (var claim in new[] { "iat", "nbf" })
                if (payload.TryGetProperty(claim, out var value) && NumericDate(value) > unixNow)
                    throw new FormatException("Token is not yet valid.");
            if (payload.TryGetProperty("exp", out var exp) && NumericDate(exp) <= unixNow)
                return (401, new { success = false, error = "Token expirado", message = "O JWT está expirado" });
            if (payload.TryGetProperty("aud", out var aud) && IsTruthy(aud))
                throw new FormatException("Unexpected audience.");
            foreach (var claim in new[] { "sub", "jti" })
                if (payload.TryGetProperty(claim, out var value) && value.ValueKind != JsonValueKind.String)
                    throw new FormatException("Invalid string claim.");

            if (string.IsNullOrEmpty(_options.JwtPassword))
                return (503, new { success = false, error = "Configuração do servidor", message = "Autenticação da API não configurada" });

            var senha = GetAlias(payload, "senha", "password");
            var datahoraValue = GetAlias(payload, "datahora", "datetime");

            if (senha.ValueKind != JsonValueKind.String || senha.GetString() != _options.JwtPassword)
                return (401, new { success = false, error = "Não autorizado", message = "Senha no token não confere" });

            if (!IsTruthy(datahoraValue))
                return (401, new { success = false, error = "Token inválido", message = "Campo datahora (ou datetime) é obrigatório no payload do JWT" });

            if (datahoraValue.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(datahoraValue.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var datahora))
                return (401, new { success = false, error = "Token inválido", message = "Data/hora no token em formato inválido" });

            // dateutil truncates precision beyond Python's six microsecond digits.
            datahora = datahora.AddTicks(-(datahora.Ticks % 10));
            if (datahora > now)
                return (401, new { success = false, error = "Token inválido", message = "Data/hora do token não pode ser no futuro" });

            var diffHours = (now - datahora).TotalHours;
            if (diffHours > _options.JwtDatetimeToleranceHours)
                return (401, new { success = false, error = "Token expirado", message = $"Data/hora do token fora do intervalo permitido (máx. {_options.JwtDatetimeToleranceHours.ToString(CultureInfo.InvariantCulture)}h)" });

            if (datahora.Date != now.Date)
                return (401, new { success = false, error = "Token inválido", message = "Data/hora do token deve ser do dia atual" });
        }
        catch
        {
            return (401, new { success = false, error = "Token inválido", message = "JWT malformado ou assinatura inválida" });
        }

        return null;
    }

    private static JsonElement GetAlias(JsonElement payload, string primary, string alias) =>
        payload.TryGetProperty(primary, out var value) && IsTruthy(value) ? value
            : payload.TryGetProperty(alias, out value) ? value : default;

    private static bool IsTruthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.False => false,
        JsonValueKind.String => value.GetString()!.Length > 0,
        JsonValueKind.Number => value.GetDouble() != 0,
        JsonValueKind.Array => value.GetArrayLength() > 0,
        JsonValueKind.Object => value.EnumerateObject().Any(),
        _ => true
    };

    private static double NumericDate(JsonElement value)
    {
        // PyJWT applies int(): truncate JSON numbers, accept integer strings only.
        if (value.ValueKind == JsonValueKind.Number)
        {
            var number = value.GetDouble();
            if (double.IsFinite(number)) return Math.Truncate(number);
        }
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean() ? 1 : 0;
        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return seconds;
        throw new FormatException("Invalid NumericDate.");
    }
}
