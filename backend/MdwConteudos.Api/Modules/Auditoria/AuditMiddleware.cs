using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Dapper;
using MdwConteudos.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace MdwConteudos.Api.Modules.Auditoria;

public sealed class AuditFileResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is FileResult && AuditRequest.Get(context.HttpContext) is { } request)
            request.FileOffered = true;
        await next();
    }
}

public sealed class AuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuditWriter writer, IFirebirdConnectionFactory db)
    {
        var module = Domain(context.Request.Path);
        if (module is null) { await next(context); return; }
        var state = new AuditRequest();
        context.Items[AuditRequest.ItemKey] = state;
        var threw = false;
        try
        {
            var endpoint = context.GetEndpoint();
            var protectedEndpoint = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
            if (protectedEndpoint && context.User.Identity?.IsAuthenticated == true)
            {
                if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                { context.Response.StatusCode = 403; return; }
                await using var conn = await db.OpenConnectionAsync(context.RequestAborted);
                var profile = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                    "SELECT PERFIL FROM USUARIO WHERE CODUSUARIO=@id AND STATUS=-1", new { id }, cancellationToken: context.RequestAborted));
                if (profile is null) { context.Response.StatusCode = 403; return; }
                var identity = new ClaimsIdentity(context.User.Claims.Where(c => c.Type != ClaimTypes.Role), context.User.Identity.AuthenticationType,
                    ClaimTypes.Name, ClaimTypes.Role);
                identity.AddClaim(new Claim(ClaimTypes.Role, profile.Trim().ToLowerInvariant()));
                context.User = new ClaimsPrincipal(identity);
            }
            await next(context);
        }
        catch { threw = true; throw; }
        finally
        {
            var code = threw ? 500 : context.Response.StatusCode;
            if (!state.SecurityRecorded && code is 401 or 403)
                await writer.TryRecordAsync(module, "acesso_negado", "negado", code == 401 ? "nao_autenticado" : "sem_permissao");
            else if (!state.SecurityRecorded && code >= 400 && (threw || !HttpMethods.IsGet(context.Request.Method)))
                await writer.TryRecordAsync(module, Action(context), "falha", code >= 500 ? "erro_interno" : "validacao");
            else if (code < 400 && state.FileOffered)
                await writer.TryRecordAsync(module, context.Request.Path.Value?.Contains("export", StringComparison.OrdinalIgnoreCase) == true ? "exportar" : "download", "iniciado");
        }
    }

    internal static string Action(HttpContext context)
    {
        if (HttpMethods.IsDelete(context.Request.Method)) return "excluir";
        if (HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method)) return "editar";
        if (HttpMethods.IsGet(context.Request.Method)) return "download";
        var path = context.Request.Path.Value ?? "";
        if (path.Contains("import", StringComparison.OrdinalIgnoreCase)) return "importar";
        if (path.EndsWith("reset-password", StringComparison.OrdinalIgnoreCase)) return "redefinir_senha";
        if (path.EndsWith("login", StringComparison.OrdinalIgnoreCase)) return "login";
        return "criar";
    }

    public static string? Domain(PathString path)
    {
        var segments = path.Value?.Trim('/').Split('/') ?? [];
        if (segments.Length < 3 || !(segments[0] == "api" && segments[1] is "web" or "v1" || segments[0] == "apiconteudos" && segments[1] == "v1")) return null;
        var area = segments[2].ToLowerInvariant();
        return area switch
        {
            "auth" => "autenticacao", "permissoes" => "permissoes", "usuarios" or "users" => "usuarios",
            "scripts" => "scripts", "paineis" => "paineis", "relatorios" => "relatorios", "variaveis" => "variaveis",
            "formulas" => "formulas", "referencias" or "normalidades" => "referencias", "pacotes" => "pacotes",
            "cadastros" or "unidades" or "linguagens" or "especialidades" or "autores" or "classificacoes" or "grupos" or "codigos" or "clientes" or "modulos" => "cadastros",
            "auditoria" => "cadastros", "orcamentos" => "orcamentos", _ => null
        };
    }
}
