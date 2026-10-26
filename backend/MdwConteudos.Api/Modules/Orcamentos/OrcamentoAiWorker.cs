using Dapper;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Permissions;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed class OrcamentoAiWorker(IServiceScopeFactory scopes, IOptions<OrcamentoAiOptions> options, ILogger<OrcamentoAiWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOne(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { logger.LogWarning("Não foi possível consultar a fila de IA de orçamentos. Verifique a migração e a conexão."); }
            try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
    public async Task<bool> RunOne(CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IFirebirdConnectionFactory>();
        var permission = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        var token = Guid.NewGuid().ToString(); OrcamentoService.AiRow? job;
        await using (var c = await db.OpenConnectionAsync(ct))
        {
            // Never re-send an interrupted request automatically: it may already have incurred cost.
            await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOANALISE SET STATUS='erro',ERRO='Execução interrompida. Solicite nova análise explicitamente.',DTHRFIM=@now WHERE STATUS='executando' AND DTHRINICIO<@expired", new { now = DateTime.UtcNow, expired = DateTime.UtcNow.AddSeconds(-Math.Clamp(options.Value.TimeoutSeconds, 15, 300) - 60) }, cancellationToken: ct));
            job = await c.QuerySingleOrDefaultAsync<OrcamentoService.AiRow>(new CommandDefinition(OrcamentoService.AiSql.Replace("SELECT ", "SELECT FIRST 1 ") + " WHERE STATUS='pendente' ORDER BY CODORCAMENTOANALISE", cancellationToken: ct));
            if (job is null) return false;
            if (await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOANALISE SET STATUS='executando',DTHRINICIO=@now,CHAVEEXECUCAO=@token WHERE CODORCAMENTOANALISE=@id AND STATUS='pendente'", new { now = DateTime.UtcNow, token, id = job.Id }, cancellationToken: ct)) != 1) return false;
        }
        string? result = null; string? error = null;
        Task<QuoteAiSuggestion>? analysisTask = null;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct); cancel.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.TimeoutSeconds, 15, 300)));
        try
        {
            if (job.Model != options.Value.Model || !await Authorized(db, permission, job.ActorId, ct)) throw new OrcamentoException(403, "Modelo alterado ou solicitante sem permissão. Solicite uma nova análise.");
            var task = analysisTask = scope.ServiceProvider.GetRequiredService<IQuoteAiProvider>().Analyze(OrcamentoJson.Read<QuoteAiInput>(job.Input), cancel.Token);
            while (!task.IsCompleted)
            {
                if (await Task.WhenAny(task, Task.Delay(1000, cancel.Token)) == task) break;
                cancel.Token.ThrowIfCancellationRequested();
                await using var check = await db.OpenConnectionAsync(ct);
                if (await check.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOANALISE WHERE CODORCAMENTOANALISE=@id AND STATUS='executando' AND CHAVEEXECUCAO=@token", new { id = job.Id, token }, cancellationToken: ct)) == 0)
                { cancel.Cancel(); try { await task; } catch { } return true; }
            }
            var suggestion = await task;
            if (!await Authorized(db, permission, job.ActorId, ct)) throw new OrcamentoException(403, "Permissão do solicitante revogada durante a análise.");
            result = OrcamentoJson.Write(QuoteAiSuggestion.ValidateForInput(suggestion, OrcamentoJson.Read<QuoteAiInput>(job.Input)));
        }
        catch (OperationCanceledException) { error = "Análise interrompida ou tempo limite excedido. Nenhum item foi aplicado."; }
        catch (OrcamentoException ex) { error = ex.Message; }
        catch { error = "Falha na análise. Confira documentos, instalação do bridge e configuração do Composer."; }
        finally { cancel.Cancel(); if (analysisTask is not null) { try { await analysisTask; } catch { } } }
        await using (var c = await db.OpenConnectionAsync(CancellationToken.None))
        await using (var tx = await c.BeginTransactionAsync())
        {
            var status = result is null ? "erro" : "concluida";
            if (await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOANALISE SET STATUS=@status,RESULTADO=@result,ERRO=@error,DTHRFIM=@now WHERE CODORCAMENTOANALISE=@id AND STATUS='executando' AND CHAVEEXECUCAO=@token", new { status, result = new QuoteTextBlob(result), error, now = DateTime.UtcNow, id = job.Id, token }, tx)) == 1)
                await c.ExecuteAsync(new CommandDefinition("INSERT INTO AUDITORIA_EVENTO(OCCURRED_UTC,MODULE,ACTION,RESULT,ENTITY_TYPE,ENTITY_ID,SUMMARY,ACTOR_ID,ACTOR_KIND,ORIGIN) VALUES(@now,'orcamentos','resultado_ia',@outcome,'ORCAMENTOANALISE',@id,'Processamento de análise de orçamento',@actor,'sistema','worker')", new { now = DateTime.UtcNow, outcome = result is null ? "falha" : "sucesso", id = job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), actor = job.ActorId }, tx));
            await tx.CommitAsync();
        }
        return true;
    }
    private static async Task<bool> Authorized(IFirebirdConnectionFactory db, IPermissionService permission, int id, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct);
        var role = await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT PERFIL FROM USUARIO WHERE CODUSUARIO=@id AND STATUS=-1", new { id }, cancellationToken: ct));
        return role is not null && await permission.HasPermissionAsync(id, role, "orcamentos", "usar_ia", ct) && await permission.HasPermissionAsync(id, role, "orcamentos", "elaborar", ct) && await permission.HasPermissionAsync(id, role, "orcamentos", "visualizar", ct);
    }
}
