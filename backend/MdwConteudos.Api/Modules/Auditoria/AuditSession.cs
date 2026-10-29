using System.Security.Claims;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Routing;

namespace MdwConteudos.Api.Modules.Auditoria;

public sealed class AuditRequest
{
    public string OperationId { get; } = Guid.NewGuid().ToString("N");
    public bool FileOffered { get; set; }
    public bool SecurityRecorded { get; set; }
    public const string ItemKey = "MDW.AuditRequest";
    public static AuditRequest? Get(HttpContext? context) => context?.Items[ItemKey] as AuditRequest;
}

public static class AuditSession
{
    public static Task<int> SuppressAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, bool suppress) =>
        connection.ExecuteScalarAsync<int>("SELECT RDB$SET_CONTEXT('USER_TRANSACTION','AUD_SUPPRESS',@value) FROM RDB$DATABASE",
            new { value = suppress ? "1" : (string?)null }, transaction);
    private static readonly string[] Keys = ["AUD_ENABLED", "AUD_ACTOR_ID", "AUD_ACTOR_NAME", "AUD_ACTOR_KIND", "AUD_ORIGIN", "AUD_CORRELATION", "AUD_OPERATION", "AUD_METHOD", "AUD_PATH", "AUD_IP", "AUD_IMPORT", "AUD_PASSWORD_ACTION"];
    private static readonly string SetSql = "SELECT " + string.Join(",", Keys.Select((key, i) => $"RDB$SET_CONTEXT('USER_SESSION','{key}',CAST(@v{i} AS VARCHAR(1024) CHARACTER SET UTF8)) AS C{i}")) + " FROM RDB$DATABASE";

    // Called on EVERY checkout, including non-HTTP work, so pooled connections cannot retain another actor.
    public static async Task InitializeAsync(FbConnection conn, HttpContext? context, CancellationToken ct)
    {
        var request = AuditRequest.Get(context);
        var values = Keys.ToDictionary(k => k, _ => (string?)null);
        if (request is not null && context is not null)
        {
            values["AUD_ENABLED"] = "1";
            values["AUD_ACTOR_KIND"] = "anonimo";
            values["AUD_ORIGIN"] = "web";
            if (context.User.Identity?.IsAuthenticated == true && int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                var name = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                    "SELECT NOME FROM USUARIO WHERE CODUSUARIO=@id AND STATUS=-1", new { id }, cancellationToken: ct));
                if (name is not null)
                {
                    values["AUD_ACTOR_ID"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    values["AUD_ACTOR_NAME"] = Clip(name);
                    values["AUD_ACTOR_KIND"] = "usuario";
                }
            }
            else if (context.Items.ContainsKey("MDW.ValidPartner"))
            {
                values["AUD_ACTOR_KIND"] = "integracao";
                values["AUD_ORIGIN"] = "integracao";
            }
            values["AUD_CORRELATION"] = Clip(context.TraceIdentifier, 128);
            values["AUD_OPERATION"] = request.OperationId;
            values["AUD_METHOD"] = Clip(context.Request.Method, 10);
            // Route template rather than raw path: tokens/identifiers in unusual routes cannot leak.
            values["AUD_PATH"] = Clip((context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText is { } route ? "/" + route.TrimStart('/') : "/", 255);
            values["AUD_IP"] = Clip(context.Connection.RemoteIpAddress?.ToString(), 64);
            values["AUD_IMPORT"] = context.Request.Path.Value?.Contains("import", StringComparison.OrdinalIgnoreCase) == true ? "1" : null;
            values["AUD_PASSWORD_ACTION"] = context.Request.Path.Value?.EndsWith("/reset-password", StringComparison.OrdinalIgnoreCase) == true ? "redefinir_senha" : null;
        }
        var p = new DynamicParameters();
        for (var i = 0; i < Keys.Length; i++) p.Add("v" + i, new AuditText(values[Keys[i]]));
        await conn.QuerySingleAsync(new CommandDefinition(SetSql, p, cancellationToken: ct));
    }

    internal static string? Clip(string? value, int max = 255) => value is null ? null : value[..Math.Min(max, value.Length)];
}
