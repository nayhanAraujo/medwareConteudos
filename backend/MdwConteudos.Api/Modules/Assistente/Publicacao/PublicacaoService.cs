using System.Data;
using System.Text;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Assistente.Importacao;

namespace MdwConteudos.Api.Modules.Assistente.Publicacao;

public sealed class PublicacaoService(
    IFirebirdConnectionFactory sourceFactory,
    IAssistantFirebirdConnectionFactory targetFactory,
    IOptions<PublicacaoOptions> options,
    ILogger<PublicacaoService>? logger = null)
{
    public bool Enabled => options.Value.Enabled;
    private string Origin => options.Value.SourceKey;

    public void RequireEnabled()
    {
        if (!Enabled) throw new InvalidOperationException("Publicacao desabilitada. Aplique as migracoes e configure a origem antes de habilitar.");
        if (string.IsNullOrWhiteSpace(Origin) || Origin.Length > 64)
            throw new InvalidOperationException("Configure PublicacaoAssistente:SourceKey com um identificador estavel de ate 64 caracteres.");
    }

    public async Task<object> Status(CancellationToken ct)
    {
        if (!Enabled) return new { enabled = false };
        RequireEnabled();
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        var packages = await source.QueryAsync(new CommandDefinition("SELECT CODPACOTE AS Id, NOME AS Nome FROM PACOTES ORDER BY NOME", cancellationToken: ct));
        var mappings = await source.QueryAsync(new CommandDefinition("SELECT CODPACOTE AS Pacote, CODESPECIALIDADE AS Especialidade FROM ASS_PACOTE_MAPA", cancellationToken: ct));
        var scriptMappings = await source.QueryAsync(new CommandDefinition("SELECT CODSCRIPTLAUDO AS Script, CODESPECIALIDADE AS Especialidade FROM ASS_SCRIPT_MAPA", cancellationToken: ct));
        var specialties = await target.QueryAsync(new CommandDefinition("SELECT CODESPECIALIDADE AS Id, DESCRICAO AS Nome FROM ESPECIALIDADE ORDER BY DESCRICAO", cancellationToken: ct));
        return new { enabled = true, packages, mappings, scriptMappings, specialties };
    }

    public async Task<object> List(int page, int? scriptId, CancellationToken ct, int[]? ids = null)
    {
        if (!Enabled) return new { items = Array.Empty<object>(), total = 0 };
        RequireEnabled();
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        var where = scriptId.HasValue ? "WHERE q.CODSCRIPTLAUDO=@scriptId" : ids is not null ? "WHERE q.CODSCRIPTLAUDO IN @ids" : "";
        var args = new { skip = (Math.Max(1, page) - 1) * 30, scriptId, ids };
        var items = await source.QueryAsync(new CommandDefinition($"""
            SELECT FIRST 30 SKIP @skip q.CODSCRIPTLAUDO AS Id, s.NOME AS Nome,
            q.REVISAO, q.ESTADO, q.TENTATIVAS, q.ERRO, q.PUBLICADO_EM, q.CODDESTINO
            FROM ASS_PUBLICACAO q LEFT JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO=q.CODSCRIPTLAUDO
            {where} ORDER BY q.REVISAO DESC
            """, args, cancellationToken: ct));
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        var suspended = (await target.QueryAsync<int>(new CommandDefinition("SELECT CODORIGEM FROM CON_PUBLICACAO WHERE ORIGEM=@Origin AND SUSPENSO=1", new { Origin }, cancellationToken: ct))).ToHashSet();
        foreach (IDictionary<string, object> item in items)
            if (suspended.Contains(Convert.ToInt32(item["ID"]))) item["ESTADO"] = "suspenso";
        var total = await source.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM ASS_PUBLICACAO q {where}", args, cancellationToken: ct));
        return new { items, total };
    }

    public async Task SaveMapping(int package, int[] ids, CancellationToken ct)
    {
        RequireEnabled();
        ids = await ValidateSpecialties(ids, ct);
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        await using var tx = await source.BeginTransactionAsync(ct);
        if (await source.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM PACOTES WHERE CODPACOTE=@package", new { package }, tx, cancellationToken: ct)) != 1)
            throw new InvalidOperationException("Pacote inexistente.");
        await source.ExecuteAsync(new CommandDefinition("DELETE FROM ASS_PACOTE_MAPA WHERE CODPACOTE=@package", new { package }, tx, cancellationToken: ct));
        foreach (var id in ids)
            await source.ExecuteAsync(new CommandDefinition("INSERT INTO ASS_PACOTE_MAPA (CODPACOTE,CODESPECIALIDADE) VALUES (@package,@id)", new { package, id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public async Task<object> PackageScripts(int package, int page, string? search, CancellationToken ct)
    {
        if (!Enabled) return new { items = Array.Empty<object>(), total = 0 };
        RequireEnabled();
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        var where = new List<string> { "s.CODPACOTE=@package" };
        var parameters = new DynamicParameters(new { package, skip = (Math.Max(1, page) - 1) * 30 });
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(UPPER(s.NOME) CONTAINING UPPER(@search) OR CAST(s.CODSCRIPTLAUDO AS VARCHAR(20)) CONTAINING @search)");
            parameters.Add("search", search.Trim());
        }
        var whereSql = string.Join(" AND ", where);
        var total = await source.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM SCRIPTLAUDO s WHERE {whereSql}", parameters, cancellationToken: ct));
        var rows = (await source.QueryAsync<PackageScriptRow>(new CommandDefinition($"""
            SELECT FIRST 30 SKIP @skip s.CODSCRIPTLAUDO AS Id, s.NOME AS Nome, s.SISTEMA AS Sistema,
            s.LINGUAGEM AS Linguagem, s.ATIVO AS Ativo, q.ESTADO AS Estado, q.CODDESTINO AS CodDestino
            FROM SCRIPTLAUDO s LEFT JOIN ASS_PUBLICACAO q ON q.CODSCRIPTLAUDO=s.CODSCRIPTLAUDO
            WHERE {whereSql}
            ORDER BY s.NOME
            """, parameters, cancellationToken: ct))).ToList();
        var ids = rows.Select(x => x.Id).ToArray();
        var packageSpecialties = (await source.QueryAsync<int>(new CommandDefinition("SELECT CODESPECIALIDADE FROM ASS_PACOTE_MAPA WHERE CODPACOTE=@package ORDER BY CODESPECIALIDADE", new { package }, cancellationToken: ct))).ToArray();
        var specificRows = ids.Length == 0
            ? []
            : (await source.QueryAsync<ScriptMappingRow>(new CommandDefinition("SELECT CODSCRIPTLAUDO AS Script, CODESPECIALIDADE AS Especialidade FROM ASS_SCRIPT_MAPA WHERE CODSCRIPTLAUDO IN @ids ORDER BY CODESPECIALIDADE", new { ids }, cancellationToken: ct))).ToList();
        var specificByScript = specificRows.GroupBy(x => x.Script).ToDictionary(g => g.Key, g => g.Select(x => x.Especialidade).ToArray());
        var items = rows.Select(row =>
        {
            specificByScript.TryGetValue(row.Id, out var specific);
            var effective = specific is { Length: > 0 } ? specific : packageSpecialties;
            return new
            {
                row.Id,
                row.Nome,
                row.Sistema,
                row.Linguagem,
                row.Ativo,
                row.Estado,
                row.CodDestino,
                UsaRegraEspecifica = specific is { Length: > 0 },
                EspecialidadesEspecificas = specific ?? [],
                EspecialidadesEfetivas = effective
            };
        }).ToArray();
        return new { items, total, packageSpecialties };
    }

    public async Task SaveScriptMapping(int package, int script, int[] ids, CancellationToken ct)
    {
        RequireEnabled();
        ids = await ValidateSpecialties(ids, ct);
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        await using var tx = await source.BeginTransactionAsync(ct);
        if (await source.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@script AND CODPACOTE=@package", new { script, package }, tx, cancellationToken: ct)) != 1)
            throw new InvalidOperationException("Script inexistente neste pacote.");
        await source.ExecuteAsync(new CommandDefinition("DELETE FROM ASS_SCRIPT_MAPA WHERE CODSCRIPTLAUDO=@script", new { script }, tx, cancellationToken: ct));
        foreach (var id in ids)
            await source.ExecuteAsync(new CommandDefinition("INSERT INTO ASS_SCRIPT_MAPA (CODSCRIPTLAUDO,CODESPECIALIDADE) VALUES (@script,@id)", new { script, id }, tx, cancellationToken: ct));
        if (ids.Length == 0)
            await source.ExecuteAsync(new CommandDefinition("EXECUTE PROCEDURE ASS_ENFILEIRAR(@script)", new { script }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public async Task SaveBulkScriptMapping(int package, int[] scripts, int[] ids, CancellationToken ct)
    {
        RequireEnabled();
        scripts = scripts.Distinct().Where(x => x > 0).ToArray();
        if (scripts.Length == 0) throw new InvalidOperationException("Selecione pelo menos um script.");
        ids = await ValidateSpecialties(ids, ct);
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        await using var tx = await source.BeginTransactionAsync(ct);
        var existing = (await source.QueryAsync<int>(new CommandDefinition("SELECT CODSCRIPTLAUDO FROM SCRIPTLAUDO WHERE CODPACOTE=@package AND CODSCRIPTLAUDO IN @scripts", new { package, scripts }, tx, cancellationToken: ct))).ToHashSet();
        if (existing.Count != scripts.Length) throw new InvalidOperationException("Um ou mais scripts nao pertencem ao pacote selecionado.");
        foreach (var script in scripts)
        {
            await source.ExecuteAsync(new CommandDefinition("DELETE FROM ASS_SCRIPT_MAPA WHERE CODSCRIPTLAUDO=@script", new { script }, tx, cancellationToken: ct));
            foreach (var id in ids)
                await source.ExecuteAsync(new CommandDefinition("INSERT INTO ASS_SCRIPT_MAPA (CODSCRIPTLAUDO,CODESPECIALIDADE) VALUES (@script,@id)", new { script, id }, tx, cancellationToken: ct));
            if (ids.Length == 0)
                await source.ExecuteAsync(new CommandDefinition("EXECUTE PROCEDURE ASS_ENFILEIRAR(@script)", new { script }, tx, cancellationToken: ct));
        }
        await tx.CommitAsync(ct);
    }

    public async Task Enqueue(int id, bool resume, CancellationToken ct)
    {
        RequireEnabled();
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        if (id <= 0 || await source.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@id", new { id }, cancellationToken: ct)) == 0
            && await source.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ASS_PUBLICACAO WHERE CODSCRIPTLAUDO=@id", new { id }, cancellationToken: ct)) == 0)
            throw new InvalidOperationException("Script de origem inexistente.");
        // Always persist the request before releasing a local suspension. A later retry is harmless.
        await source.ExecuteAsync(new CommandDefinition("EXECUTE PROCEDURE ASS_ENFILEIRAR(@id)", new { id }, cancellationToken: ct));
        if (resume)
        {
            await using var target = await targetFactory.OpenConnectionAsync(ct);
            await target.ExecuteAsync(new CommandDefinition("UPDATE CON_PUBLICACAO SET SUSPENSO=0 WHERE ORIGEM=@Origin AND CODORIGEM=@id", new { Origin, id }, cancellationToken: ct));
            await source.ExecuteAsync(new CommandDefinition("EXECUTE PROCEDURE ASS_ENFILEIRAR(@id)", new { id }, cancellationToken: ct));
        }
    }

    public async Task<IReadOnlyList<dynamic>> TargetOrigins(int[] ids, CancellationToken ct)
    {
        if (!Enabled || ids.Length == 0) return [];
        RequireEnabled();
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        return (await target.QueryAsync(new CommandDefinition("SELECT CODDESTINO AS Id, CODORIGEM AS Origem FROM CON_PUBLICACAO WHERE ORIGEM=@Origin AND CODDESTINO IN @ids", new { Origin, ids }, cancellationToken: ct))).ToList();
    }

    public async Task<AssistenteImportacaoResult> ImportSource(int id, int[] specialties, int[] procedures, CancellationToken ct)
    {
        await Enqueue(id, false, ct);
        await ProcessNext(ct, id);
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        var state = await source.QuerySingleAsync(new CommandDefinition("SELECT ESTADO,ERRO,CODDESTINO FROM ASS_PUBLICACAO WHERE CODSCRIPTLAUDO=@id", new { id }, cancellationToken: ct));
        if ((string)state.ESTADO != "sincronizado" || state.CODDESTINO is null)
            throw new AssistenteImportacaoException((string?)state.ERRO ?? "Publicacao suspensa ou pendente. Consulte a central de publicacao.");
        int destination = (int)state.CODDESTINO;
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        await using var tx = await target.BeginTransactionAsync(ct);
        var mrd = await target.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT CODMRD FROM CON_PUBLICACAO WHERE ORIGEM=@Origin AND CODORIGEM=@id WITH LOCK", new { Origin, id }, tx, cancellationToken: ct));
        foreach (var specialty in specialties.Distinct())
            await target.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO SCRIPTLAUDO_ESPECIALIDADE (CODSCRIPTLAUDO,CODESPECIALIDADE) VALUES (@destination,@specialty) MATCHING (CODSCRIPTLAUDO,CODESPECIALIDADE)", new { destination, specialty }, tx, cancellationToken: ct));
        foreach (var procedure in procedures.Distinct())
            await target.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO PROCEDIMENTO_SCRIPTLAUDO (CODSCRIPTLAUDO,CODPROCEDIMENTO) VALUES (@destination,@procedure) MATCHING (CODSCRIPTLAUDO,CODPROCEDIMENTO)", new { destination, procedure }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return new AssistenteImportacaoResult(destination, mrd, specialties, procedures.Length);
    }

    public async Task<object> Candidates(int id, CancellationToken ct)
    {
        RequireEnabled();
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        await using var tx = await source.BeginTransactionAsync(ct);
        var payload = await Load(source, tx, id, ct) ?? throw new InvalidOperationException("Script de origem inexistente.");
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        var rows = await target.QueryAsync(new CommandDefinition("""
            SELECT s.CODSCRIPTLAUDO AS Id,s.TITULO AS Nome,s.TIPOSCRIPT AS Tipo,s.ESTRUTURASCRIPT AS Conteudo
            FROM SCRIPTLAUDO s WHERE UPPER(TRIM(s.TITULO))=UPPER(@Title)
            AND NOT EXISTS (SELECT 1 FROM CON_PUBLICACAO p WHERE p.CODDESTINO=s.CODSCRIPTLAUDO)
            """, payload, cancellationToken: ct));
        var mrds = await target.QueryAsync(new CommandDefinition("""
            SELECT sp.CODSCRIPTLAUDO AS ScriptId,p.CODPAGFOTOS AS Id,p.TITULO AS Nome
            FROM SCRIPTLAUDO_PAGFOTOS sp JOIN PAGFOTOS p ON p.CODPAGFOTOS=sp.CODPAGFOTOS
            WHERE NOT EXISTS (SELECT 1 FROM CON_PUBLICACAO c WHERE c.CODMRD=p.CODPAGFOTOS)
            AND NOT EXISTS (SELECT 1 FROM SCRIPTLAUDO_PAGFOTOS otherlink WHERE otherlink.CODPAGFOTOS=p.CODPAGFOTOS AND otherlink.CODSCRIPTLAUDO<>sp.CODSCRIPTLAUDO)
            """, cancellationToken: ct));
        return rows.Select(r => new { id = (int)r.ID, nome = (string)r.NOME, tipo = (short)r.TIPO,
            conteudoIgual = string.Equals(Convert.ToString(r.CONTEUDO), payload.Content, StringComparison.Ordinal), tipoIgual = (short)r.TIPO == payload.Type,
            mrds = payload.MrdContent is null ? [] : mrds.Where(m => (int)m.SCRIPTID == (int)r.ID).Select(m => new { id = (int)m.ID, nome = (string)m.NOME }).ToArray() }).ToList();
    }

    public async Task Reconcile(int id, int destination, CancellationToken ct, int? mrdDestination = null)
    {
        RequireEnabled();
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        await using var sourceTx = await source.BeginTransactionAsync(ct);
        await source.ExecuteAsync(new CommandDefinition("EXECUTE PROCEDURE ASS_ENFILEIRAR(@id)", new { id }, sourceTx, cancellationToken: ct));
        var payload = await Load(source, sourceTx, id, ct) ?? throw new InvalidOperationException("Script de origem inexistente.");
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        await using var tx = await target.BeginTransactionAsync(ct);
        var existing = await target.QueryFirstOrDefaultAsync(new CommandDefinition("SELECT TITULO,STATUS FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@destination WITH LOCK", new { destination }, tx, cancellationToken: ct));
        if (existing is null) throw new InvalidOperationException("Modelo de destino inexistente.");
        if (!string.Equals(((string)existing.TITULO).Trim(), payload.Title.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O titulo mudou. Revise os candidatos antes de confirmar.");
        var count = await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM CON_PUBLICACAO WHERE (ORIGEM=@Origin AND CODORIGEM=@id) OR CODDESTINO=@destination", new { Origin, id, destination }, tx, cancellationToken: ct));
        if (count != 0) throw new InvalidOperationException("Ja existe uma origem associada ou suspensa. Use a retomada para copias excluidas.");
        if (mrdDestination.HasValue)
        {
            var linked = await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO=@destination AND CODPAGFOTOS=@mrdDestination", new { destination, mrdDestination }, tx, cancellationToken: ct));
            var shared = await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO<>@destination AND CODPAGFOTOS=@mrdDestination", new { destination, mrdDestination }, tx, cancellationToken: ct));
            var managed = await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM CON_PUBLICACAO WHERE CODMRD=@mrdDestination", new { mrdDestination }, tx, cancellationToken: ct));
            if (payload.MrdContent is null || linked != 1 || shared != 0 || managed != 0)
                throw new InvalidOperationException("O MRD selecionado nao esta disponivel para conciliacao. Revise os vinculos.");
        }
        await target.ExecuteAsync(new CommandDefinition("INSERT INTO CON_PUBLICACAO (ORIGEM,CODORIGEM,CODDESTINO,CODMRD,BLOQUEADO) VALUES (@Origin,@id,@destination,@mrdDestination,@blocked)", new { Origin, id, destination, mrdDestination, blocked = Convert.ToInt32(existing.STATUS) == 0 ? 1 : 0 }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        await sourceTx.CommitAsync(ct);
    }

    public async Task<bool> ProcessNext(CancellationToken ct, int? scriptId = null)
    {
        RequireEnabled();
        await using var source = await sourceFactory.OpenConnectionAsync(ct);
        // The queue row stays locked through the destination commit and acknowledgement.
        // Writers and other workers cannot publish a newer revision of this script concurrently.
        await using var tx = await source.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var job = await source.QueryFirstOrDefaultAsync<Job>(new CommandDefinition($"""
            SELECT FIRST 1 CODSCRIPTLAUDO AS Id, REVISAO AS Revision, TENTATIVAS AS Attempts
            FROM ASS_PUBLICACAO WHERE ESTADO IN ('pendente','repetir') AND PROXIMA<=CURRENT_TIMESTAMP
            {(scriptId.HasValue ? "AND CODSCRIPTLAUDO=@scriptId" : "")}
            ORDER BY REVISAO WITH LOCK
            """, new { scriptId }, transaction: tx, commandTimeout: 15, cancellationToken: ct));
        if (job is null) return false;
        try
        {
            var payload = await Load(source, tx, job.Id, ct);
            var result = await Publish(job, payload, ct);
            await source.ExecuteAsync(new CommandDefinition("""
                UPDATE ASS_PUBLICACAO SET ESTADO=@State, CODDESTINO=@Id, ERRO=NULL,
                PUBLICADO_EM=CURRENT_TIMESTAMP WHERE CODSCRIPTLAUDO=@SourceId
                """, new { result.State, result.Id, SourceId = job.Id }, tx, cancellationToken: ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var validation = ex is InvalidOperationException or AssistenteImportacaoException;
            var error = validation ? ex.Message : "Falha temporaria ao publicar. O processamento sera repetido; consulte os logs da API.";
            logger?.LogWarning(ex, "Publicacao do script {ScriptId} falhou na revisao {Revision}.", job.Id, job.Revision);
            await source.ExecuteAsync(new CommandDefinition("""
                UPDATE ASS_PUBLICACAO SET ESTADO=@state, TENTATIVAS=TENTATIVAS+1,
                ERRO=@error, PROXIMA=DATEADD(@delay SECOND TO CURRENT_TIMESTAMP) WHERE CODSCRIPTLAUDO=@id
                """, new { state = validation ? "falha" : "repetir", error = error[..Math.Min(error.Length, 1000)], delay = RetrySeconds(job.Attempts), id = job.Id }, tx, cancellationToken: ct));
        }
        await tx.CommitAsync(ct);
        return true;
    }

    public static int RetrySeconds(int attempts) => (int)Math.Min(3600, 10 * Math.Pow(2, Math.Clamp(attempts, 0, 9)));

    private async Task<Payload?> Load(FbConnection source, IDbTransaction tx, int id, CancellationToken ct)
    {
        var row = await source.QueryFirstOrDefaultAsync<SourceScript>(new CommandDefinition("""
            SELECT NOME AS Title, SISTEMA AS SystemName, LINGUAGEM AS Language, CODPACOTE AS Package,
            ATIVO AS Active, ARQUIVO_JSON AS Json, DLL AS Dll FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@id
            """, new { id }, tx, cancellationToken: ct));
        if (row is null) return null;
        if (row.Active != 1) return new Payload(row.Title, 0, null, null, null, [], null, false);
        var specialties = (await source.QueryAsync<int>(new CommandDefinition("SELECT CODESPECIALIDADE FROM ASS_SCRIPT_MAPA WHERE CODSCRIPTLAUDO=@id", new { id }, tx, cancellationToken: ct))).ToArray();
        if (specialties.Length == 0)
            specialties = (await source.QueryAsync<int>(new CommandDefinition("SELECT CODESPECIALIDADE FROM ASS_PACOTE_MAPA WHERE CODPACOTE=@Package", row, tx, cancellationToken: ct))).ToArray();
        if (specialties.Length == 0) throw new InvalidOperationException("Associe o script ou o pacote a pelo menos uma especialidade antes de publicar.");
        var versions = (await source.QueryAsync<VersionFiles>(new CommandDefinition("""
            SELECT CODVERSAO AS Id, ARQUIVO_JSON AS Json, ARQUIVO_DLL AS Dll
            FROM SCRIPTVERSOES WHERE CODSCRIPTLAUDO=@id AND ATIVO='T'
            """, new { id }, tx, cancellationToken: ct))).ToList();
        if (versions.Count > 1) throw new InvalidOperationException("O script possui mais de uma versao ativa.");
        var version = versions.SingleOrDefault();
        var ux = string.Equals(row.SystemName?.Trim(), "Laudos UX", StringComparison.OrdinalIgnoreCase);
        if (!ux && !string.Equals(row.SystemName?.Trim(), "Laudos Flex", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Somente Laudos UX ou Laudos Flex podem ser publicados.");
        var bytes = Bytes(version is null ? (ux ? row.Json : row.Dll) : (ux ? version.Json : version.Dll));
        if (bytes.Length == 0) throw new InvalidOperationException("A versao ativa (ou cadastro sem versoes) nao possui DLL/JSON.");
        var type = AssistenteScriptEncoding.ResolveTipoScript(row.SystemName!, row.Language, bytes);
        var mrd = await source.QueryFirstOrDefaultAsync<MrdFiles>(new CommandDefinition(version is null
            ? "SELECT FIRST 1 NOME_ARQUIVO AS Name, ARQUIVO_MRD AS Content FROM SCRIPTLAUDOMRD WHERE CODSCRIPTLAUDO=@id AND PADRAO='T' ORDER BY CODSCRIPTMRD"
            : "SELECT FIRST 1 NOME_ARQUIVO AS Name, ARQUIVO_MRD AS Content FROM SCRIPTVERSAOMRD WHERE CODVERSAO=@id AND PADRAO='T' ORDER BY CODVERSAOMRD",
            new { id = version?.Id ?? id }, tx, cancellationToken: ct));
        if (mrd is null && type != 3) throw new InvalidOperationException("O script Flex nao possui MRD padrao na versao selecionada.");
        return new Payload(AssistenteImportacaoValidator.ValidateTitle(row.Title, "Titulo do script", 252), type,
            AssistenteScriptEncoding.PrepareFromReferencias(type, bytes),
            mrd is null ? null : AssistenteImportacaoValidator.ValidateTitle(Path.GetFileNameWithoutExtension(mrd.Name), "Titulo MRD", 128),
            mrd is null ? null : AssistenteImportacaoValidator.PrepareMrd(mrd.Name, Bytes(mrd.Content)), specialties, version?.Id, true);
    }

    private async Task<(string State, int? Id)> Publish(Job job, Payload? payload, CancellationToken ct)
    {
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        await using var tx = await target.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var key = new { Origin, SourceId = job.Id };
        await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT RDB$SET_CONTEXT('USER_TRANSACTION','PUBLICACAO','1') FROM RDB$DATABASE", transaction: tx, cancellationToken: ct));
        var control = await target.QueryFirstOrDefaultAsync<Control>(new CommandDefinition("""
            SELECT CODDESTINO AS Id, CODMRD AS MrdId, REVISAO AS Revision, SUSPENSO AS Suspended, BLOQUEADO AS Blocked
            FROM CON_PUBLICACAO WHERE ORIGEM=@Origin AND CODORIGEM=@SourceId WITH LOCK
            """, key, tx, cancellationToken: ct));
        if (control?.Suspended == 1) return ("suspenso", control.Id);
        if (control is not null && control.Revision >= job.Revision) return ("sincronizado", control.Id);
        if (control is null)
        {
            await target.ExecuteAsync(new CommandDefinition("INSERT INTO CON_PUBLICACAO (ORIGEM,CODORIGEM) VALUES (@Origin,@SourceId)", key, tx, cancellationToken: ct));
            control = new Control();
        }
        var active = payload?.Active == true;
        if (!active)
        {
            if (control.Id.HasValue)
            {
                await target.ExecuteAsync(new CommandDefinition("UPDATE SCRIPTLAUDO SET STATUS=0, DATAMODIFICACAO=CURRENT_TIMESTAMP WHERE CODSCRIPTLAUDO=@Id", control, tx, cancellationToken: ct));
                await target.ExecuteAsync(new CommandDefinition("UPDATE PAGFOTOS SET STATUS=0 WHERE CODPAGFOTOS IN (SELECT CODPAGFOTOS FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO=@Id)", control, tx, cancellationToken: ct));
            }
        }
        else
        {
            foreach (var id in payload!.Specialties)
                if (await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ESPECIALIDADE WHERE CODESPECIALIDADE=@id", new { id }, tx, cancellationToken: ct)) != 1)
                    throw new InvalidOperationException($"Especialidade {id} nao existe mais no Assistente.");
            var p = new DynamicParameters(new { payload.Title, payload.Type, Status = control.Blocked == 1 ? 0 : -1, control.Id });
            p.Add("Content", new TextBlob(payload.Content!));
            if (!control.Id.HasValue)
            {
                if (await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM SCRIPTLAUDO WHERE UPPER(TRIM(TITULO))=UPPER(@Title)", payload, tx, cancellationToken: ct)) > 0)
                    throw new InvalidOperationException("Existe um modelo com este titulo. Confirme a conciliacao antes de publicar.");
                control.Id = await target.ExecuteScalarAsync<int>(new CommandDefinition("INSERT INTO SCRIPTLAUDO (TITULO,TIPOSCRIPT,ESTRUTURASCRIPT,STATUS,DATAMODIFICACAO) VALUES (@Title,@Type,@Content,@Status,CURRENT_TIMESTAMP) RETURNING CODSCRIPTLAUDO", p, tx, cancellationToken: ct));
            }
            else
                await target.ExecuteAsync(new CommandDefinition("UPDATE SCRIPTLAUDO SET TITULO=@Title,TIPOSCRIPT=@Type,ESTRUTURASCRIPT=@Content,STATUS=@Status,DATAMODIFICACAO=CURRENT_TIMESTAMP WHERE CODSCRIPTLAUDO=@Id", p, tx, cancellationToken: ct));
            if (payload.MrdContent is not null)
            {
                var m = new DynamicParameters(new { Title = payload.MrdTitle, Id = control.MrdId, Status = control.Blocked == 1 ? 0 : -1 });
                m.Add("Content", new TextBlob(payload.MrdContent));
                if (control.MrdId.HasValue)
                    await target.ExecuteAsync(new CommandDefinition("UPDATE PAGFOTOS SET TITULO=@Title,ESTRUTURAPAGFOTOS=@Content WHERE CODPAGFOTOS=@Id", m, tx, cancellationToken: ct));
                else
                    control.MrdId = await target.ExecuteScalarAsync<int>(new CommandDefinition("INSERT INTO PAGFOTOS (TITULO,ESTRUTURAPAGFOTOS,STATUS) VALUES (@Title,@Content,@Status) RETURNING CODPAGFOTOS", m, tx, cancellationToken: ct));
                await target.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO SCRIPTLAUDO_PAGFOTOS (CODSCRIPTLAUDO,CODPAGFOTOS,SEQUENCIA)
                    SELECT @Id,@MrdId,COALESCE(MAX(SEQUENCIA),0)+1 FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO=@Id
                    HAVING NOT EXISTS (SELECT 1 FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO=@Id AND CODPAGFOTOS=@MrdId)
                    """, control, tx, cancellationToken: ct));
                var ownedMrd = (await target.QueryAsync<int>(new CommandDefinition("SELECT CODESPECIALIDADE FROM CON_PUBLICACAO_MRD_ESP WHERE ORIGEM=@Origin AND CODORIGEM=@SourceId", key, tx, cancellationToken: ct))).ToArray();
                foreach (var specialty in ownedMrd.Except(payload.Specialties))
                {
                    await target.ExecuteAsync(new CommandDefinition("DELETE FROM PAGFOTOS_ESPECIALIDADE WHERE CODPAGFOTOS=@MrdId AND CODESPECIALIDADE=@specialty", new { control.MrdId, specialty }, tx, cancellationToken: ct));
                    await target.ExecuteAsync(new CommandDefinition("DELETE FROM CON_PUBLICACAO_MRD_ESP WHERE ORIGEM=@Origin AND CODORIGEM=@SourceId AND CODESPECIALIDADE=@specialty", new { Origin, SourceId = job.Id, specialty }, tx, cancellationToken: ct));
                }
                foreach (var specialty in payload.Specialties)
                {
                    if (await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM PAGFOTOS_ESPECIALIDADE WHERE CODPAGFOTOS=@MrdId AND CODESPECIALIDADE=@specialty", new { control.MrdId, specialty }, tx, cancellationToken: ct)) != 0) continue;
                    await target.ExecuteAsync(new CommandDefinition("INSERT INTO PAGFOTOS_ESPECIALIDADE (CODPAGFOTOS,CODESPECIALIDADE) VALUES (@MrdId,@specialty)", new { control.MrdId, specialty }, tx, cancellationToken: ct));
                    await target.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO CON_PUBLICACAO_MRD_ESP (ORIGEM,CODORIGEM,CODESPECIALIDADE) VALUES (@Origin,@SourceId,@specialty) MATCHING (ORIGEM,CODORIGEM,CODESPECIALIDADE)", new { Origin, SourceId = job.Id, specialty }, tx, cancellationToken: ct));
                }
            }
            else if (control.MrdId.HasValue)
                await target.ExecuteAsync(new CommandDefinition("DELETE FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO=@Id AND CODPAGFOTOS=@MrdId", control, tx, cancellationToken: ct));
            var owned = (await target.QueryAsync<int>(new CommandDefinition("SELECT CODESPECIALIDADE FROM CON_PUBLICACAO_ESP WHERE ORIGEM=@Origin AND CODORIGEM=@SourceId", key, tx, cancellationToken: ct))).ToArray();
            foreach (var specialty in owned.Except(payload.Specialties))
            {
                await target.ExecuteAsync(new CommandDefinition("DELETE FROM SCRIPTLAUDO_ESPECIALIDADE WHERE CODSCRIPTLAUDO=@Id AND CODESPECIALIDADE=@specialty", new { control.Id, specialty }, tx, cancellationToken: ct));
                await target.ExecuteAsync(new CommandDefinition("DELETE FROM CON_PUBLICACAO_ESP WHERE ORIGEM=@Origin AND CODORIGEM=@SourceId AND CODESPECIALIDADE=@specialty", new { Origin, SourceId = job.Id, specialty }, tx, cancellationToken: ct));
            }
            foreach (var specialty in payload.Specialties)
            {
                var exists = await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM SCRIPTLAUDO_ESPECIALIDADE WHERE CODSCRIPTLAUDO=@Id AND CODESPECIALIDADE=@specialty", new { control.Id, specialty }, tx, cancellationToken: ct));
                if (exists == 0)
                {
                    await target.ExecuteAsync(new CommandDefinition("INSERT INTO SCRIPTLAUDO_ESPECIALIDADE (CODSCRIPTLAUDO,CODESPECIALIDADE) VALUES (@Id,@specialty)", new { control.Id, specialty }, tx, cancellationToken: ct));
                    await target.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO CON_PUBLICACAO_ESP (ORIGEM,CODORIGEM,CODESPECIALIDADE) VALUES (@Origin,@SourceId,@specialty) MATCHING (ORIGEM,CODORIGEM,CODESPECIALIDADE)", new { Origin, SourceId = job.Id, specialty }, tx, cancellationToken: ct));
                }
            }
        }
        await target.ExecuteAsync(new CommandDefinition("""
            UPDATE CON_PUBLICACAO SET CODDESTINO=@Id,CODMRD=@MrdId,REVISAO=@Revision,CODVERSAO=@Version,
            INATIVO_ORIGEM=@Inactive WHERE ORIGEM=@Origin AND CODORIGEM=@SourceId
            """, new { control.Id, control.MrdId, job.Revision, Version = payload?.Version, Inactive = active ? 0 : 1, Origin, SourceId = job.Id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return ("sincronizado", control.Id);
    }

    private static byte[] Bytes(object? value) => value switch { byte[] b => b, string s => Encoding.UTF8.GetBytes(s), _ => [] };
    private async Task<int[]> ValidateSpecialties(int[] ids, CancellationToken ct)
    {
        ids = ids.Distinct().Where(x => x > 0).ToArray();
        await using var target = await targetFactory.OpenConnectionAsync(ct);
        foreach (var id in ids)
            if (await target.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ESPECIALIDADE WHERE CODESPECIALIDADE=@id", new { id }, cancellationToken: ct)) != 1)
                throw new InvalidOperationException($"Especialidade inexistente: {id}.");
        return ids;
    }

    private sealed record Job(int Id, long Revision, int Attempts);
    private sealed class PackageScriptRow
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string? Sistema { get; set; }
        public string? Linguagem { get; set; }
        public int Ativo { get; set; }
        public string? Estado { get; set; }
        public int? CodDestino { get; set; }
    }
    private sealed class ScriptMappingRow
    {
        public int Script { get; set; }
        public int Especialidade { get; set; }
    }
    private sealed class SourceScript
    {
        public string Title { get; set; } = "";
        public string? SystemName { get; set; }
        public string? Language { get; set; }
        public int? Package { get; set; }
        public int Active { get; set; }
        public object? Json { get; set; }
        public object? Dll { get; set; }
    }
    private sealed class VersionFiles
    {
        public int Id { get; set; }
        public object? Json { get; set; }
        public object? Dll { get; set; }
    }
    private sealed class MrdFiles
    {
        public string Name { get; set; } = "";
        public object? Content { get; set; }
    }
    private sealed record Payload(string Title, short Type, string? Content, string? MrdTitle, string? MrdContent, int[] Specialties, int? Version, bool Active);
    private sealed class Control
    {
        public int? Id { get; set; }
        public int? MrdId { get; set; }
        public long Revision { get; set; }
        public int Suspended { get; set; }
        public int Blocked { get; set; }
    }
    private sealed class TextBlob(string value) : SqlMapper.ICustomQueryParameter
    {
        public void AddParameter(IDbCommand command, string name) => command.Parameters.Add(new FbParameter(name, FbDbType.Text) { Value = value });
    }
}
