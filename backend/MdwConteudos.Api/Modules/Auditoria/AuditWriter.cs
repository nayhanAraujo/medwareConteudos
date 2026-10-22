using System.Data;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Auditoria;

public sealed class AuditWriter(IFirebirdConnectionFactory db, ILogger<AuditWriter> logger)
{
    // Security events do not change the outcome of authentication or mask the original business failure.
    // Business successes are instead captured atomically by AuditSchema's triggers.
    public async Task TryRecordAsync(string module, string action, string result, string? reason = null,
        int? actorId = null, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await using var conn = await db.OpenConnectionAsync(timeout.Token);
            string? actorName = null;
            if (actorId is not null)
                actorName = await conn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                    "SELECT NOME FROM USUARIO WHERE CODUSUARIO=@actorId AND STATUS=-1", new { actorId }, cancellationToken: timeout.Token));
            if (actorName is null) actorId = null;
            await conn.ExecuteAsync(new CommandDefinition("""
                INSERT INTO AUDITORIA_EVENTO (OCCURRED_UTC,MODULE,ACTION,RESULT,SUMMARY,ACTOR_ID,ACTOR_NAME,ACTOR_KIND,
                    ORIGIN,CORRELATION_ID,OPERATION_ID,REQUEST_METHOD,REQUEST_PATH,IP_ADDRESS,REASON_CODE)
                VALUES (CAST(CURRENT_TIMESTAMP AT TIME ZONE 'UTC' AS TIMESTAMP),@module,@action,@result,@summary,
                    COALESCE(@actorId,CAST(RDB$GET_CONTEXT('USER_SESSION','AUD_ACTOR_ID') AS INTEGER)),
                    COALESCE(@actorName,RDB$GET_CONTEXT('USER_SESSION','AUD_ACTOR_NAME')),
                    IIF(@actorId IS NULL,COALESCE(RDB$GET_CONTEXT('USER_SESSION','AUD_ACTOR_KIND'),'anonimo'),'usuario'),
                    COALESCE(RDB$GET_CONTEXT('USER_SESSION','AUD_ORIGIN'),'web'),
                    RDB$GET_CONTEXT('USER_SESSION','AUD_CORRELATION'),RDB$GET_CONTEXT('USER_SESSION','AUD_OPERATION'),
                    RDB$GET_CONTEXT('USER_SESSION','AUD_METHOD'),RDB$GET_CONTEXT('USER_SESSION','AUD_PATH'),
                    RDB$GET_CONTEXT('USER_SESSION','AUD_IP'),@reason)
                """, new { module, action, result, summary = action + " " + module, actorId, actorName = AuditSession.Clip(actorName), reason }, cancellationToken: timeout.Token));
        }
        catch
        {
            // Never log SMTP, SQL parameters, tokens, password material, raw payloads or exception messages.
            logger.LogWarning("AuditWriteFailed Module={Module} Action={Action} Result={Result}", module, action, result);
        }
    }
}
