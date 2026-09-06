using System.Data.Common;
using Dapper;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Assistente.Core;

namespace MdwConteudos.Api.Modules.Assistente.Dominios;

public interface IAssistenteDominiosService
{
    Task<object> Dashboard(CancellationToken ct);
    Task<IReadOnlyList<ScriptEspecialidadeGroup>> ListScriptEspecialidades(string? search, int? tipoScript, int? status, CancellationToken ct);
    Task<PagedResult<dynamic>> List(string domain, int page, int pageSize, string? search, int? groupId, int? tipoScript, int? status, int? especialidade, CancellationToken ct);
    Task<dynamic?> Get(string domain, int id, CancellationToken ct);
    Task<int> Create(string domain, object request, CancellationToken ct);
    Task<bool> Update(string domain, int id, object request, CancellationToken ct);
    Task<bool> Delete(string domain, int id, CancellationToken ct);
    Task<int> DeleteMany(string domain, IReadOnlyList<int> ids, CancellationToken ct);
    Task<bool> SetStatus(string domain, int id, int status, CancellationToken ct);
    Task SetLinks(string domain, int id, string relation, IReadOnlyList<int> ids, CancellationToken ct);
    Task SetSequencedLinks(string domain, int id, string relation, IReadOnlyList<SequencedLink> links, CancellationToken ct);
}

public sealed class AssistenteDominiosService(IAssistantFirebirdConnectionFactory connections) : IAssistenteDominiosService
{
    private static readonly IReadOnlyDictionary<string, Domain> Domains = new Dictionary<string, Domain>(StringComparer.OrdinalIgnoreCase)
    {
        ["especialidades"] = new("ESPECIALIDADE", "CODESPECIALIDADE", "DESCRICAO", null,
            ["GRUPO_ESPECIALIDADE:CODESPECIALIDADE", "PAGFOTOS_ESPECIALIDADE:CODESPECIALIDADE", "REFERENCIA_ESPECIALIDADE:CODESPECIALIDADE", "SCRIPTLAUDO_ESPECIALIDADE:CODESPECIALIDADE", "ESQUEMAFOTOS_ESPECIALIDADE:CODESPECIALIDADE", "PROCEDIMENTO:CODESPECIALIDADE"]),
        ["grupos"] = new("GRUPO", "CODGRUPO", "GRUPO", "STATUS",
            ["GRUPO:CODGRUPOPAI", "FRASE:CODGRUPO", "GRUPO_ESPECIALIDADE:CODGRUPO", "PROCEDIMENTO_GRUPO:CODGRUPO"]),
        ["frases"] = new("FRASE", "CODFRASE", "TITULO", "STATUS", ["PROCEDIMENTO_FRASE:CODFRASE"]),
        ["operadoras"] = new("OPERADORA", "CODOPERADORA", "NOMEFANTASIA", null, ["GRUPOOPERADORA_OPERADORA:CODOPERADORA"]),
        ["grupos-operadoras"] = new("GRUPOOPERADORA", "CODGRUPOOPERADORA", "DESCRICAO", null, ["GRUPOOPERADORA_OPERADORA:CODGRUPOOPERADORA"]),
        ["tabela-procedimentos"] = new("TABELAPROCEDIMENTO", "CODTABELAPROCEDIMENTO", "DESCRICAOTUSS", null, ["PROCEDIMENTO:CODTABELAPROCEDIMENTO"]),
        ["procedimentos"] = new("PROCEDIMENTO", "CODPROCEDIMENTO", "DESCRICAO_PROCED", "STATUS", ["PROCEDIMENTO_FRASE:CODPROCEDIMENTO", "PROCEDIMENTO_GRUPO:CODPROCEDIMENTO", "PROCEDIMENTO_SCRIPTLAUDO:CODPROCEDIMENTO"]),
        ["referencias"] = new("REFERENCIA", "CODREFERENCIA", "DESCRICAO", null, ["REFERENCIA_ESPECIALIDADE:CODREFERENCIA"]),
        ["esquemas"] = new("ESQUEMAS", "CODESQUEMA", "DESCRICAO", null, ["PAGFOTOS_ESQUEMA:CODESQUEMA", "SCRIPTLAUDO_ESQUEMA:CODESQUEMA"]),
        ["esquemas-fotos"] = new("ESQUEMAFOTOS", "CODESQUEMAFOTOS", "TITULO", null, ["ESQUEMAFOTOS_ESPECIALIDADE:CODESQUEMAFOTOS"]),
        ["scripts"] = new("SCRIPTLAUDO", "CODSCRIPTLAUDO", "TITULO", "STATUS", ["SCRIPTLAUDO_ESPECIALIDADE:CODSCRIPTLAUDO", "SCRIPTLAUDO_ESQUEMA:CODSCRIPTLAUDO", "SCRIPTLAUDO_PAGFOTOS:CODSCRIPTLAUDO", "PROCEDIMENTO_SCRIPTLAUDO:CODSCRIPTLAUDO"]),
        ["paginas-fotos"] = new("PAGFOTOS", "CODPAGFOTOS", "TITULO", "STATUS", ["PAGFOTOS_ESPECIALIDADE:CODPAGFOTOS", "PAGFOTOS_ESQUEMA:CODPAGFOTOS", "SCRIPTLAUDO_PAGFOTOS:CODPAGFOTOS"])
    };

    public async Task<object> Dashboard(CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct);
        var counts = new Dictionary<string, int>();
        foreach (var (name, d) in Domains)
            counts[name] = await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {d.Table}", cancellationToken: ct));
        return new { totalTabelas = 24, dominios = counts };
    }

    public async Task<IReadOnlyList<ScriptEspecialidadeGroup>> ListScriptEspecialidades(string? search, int? tipoScript, int? status, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct);
        var where = ScriptFilterConditions(search, tipoScript, status);
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var args = new { Search = $"%{search?.Trim().ToUpperInvariant()}%", TipoScript = tipoScript, Status = status };
        var groups = (await c.QueryAsync<ScriptEspecialidadeGroup>(new CommandDefinition($@"
            SELECT e.CODESPECIALIDADE AS Id, e.DESCRICAO AS Nome, COUNT(DISTINCT s.CODSCRIPTLAUDO) AS TotalScripts
            FROM ESPECIALIDADE e
            JOIN SCRIPTLAUDO_ESPECIALIDADE se ON se.CODESPECIALIDADE = e.CODESPECIALIDADE
            JOIN SCRIPTLAUDO s ON s.CODSCRIPTLAUDO = se.CODSCRIPTLAUDO
            {whereSql}
            GROUP BY e.CODESPECIALIDADE, e.DESCRICAO
            ORDER BY e.DESCRICAO", args, cancellationToken: ct))).ToList();

        var unlinkedConditions = new List<string>(where)
        {
            "NOT EXISTS (SELECT 1 FROM SCRIPTLAUDO_ESPECIALIDADE se_unlinked WHERE se_unlinked.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO)"
        };
        var unlinked = await c.ExecuteScalarAsync<int>(new CommandDefinition($@"
            SELECT COUNT(*)
            FROM SCRIPTLAUDO s
            WHERE {string.Join(" AND ", unlinkedConditions)}", args, cancellationToken: ct));
        if (unlinked > 0) groups.Add(new ScriptEspecialidadeGroup(0, "Sem especialidade", unlinked));
        return groups;
    }

    public async Task<PagedResult<dynamic>> List(string domain, int page, int pageSize, string? search, int? groupId, int? tipoScript, int? status, int? especialidade, CancellationToken ct)
    {
        var d = Resolve(domain); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var c = await connections.OpenConnectionAsync(ct);
        if (domain.Equals("procedimentos", StringComparison.OrdinalIgnoreCase))
            return await ListProcedimentos(c, page, pageSize, search, ct);
        if (domain.Equals("scripts", StringComparison.OrdinalIgnoreCase))
            return await ListScripts(c, page, pageSize, search, tipoScript, status, especialidade, ct);
        if (domain.Equals("frases", StringComparison.OrdinalIgnoreCase))
            return await ListFrases(c, page, pageSize, search, groupId, ct);
        var where = string.IsNullOrWhiteSpace(search) ? "" : $"WHERE UPPER(COALESCE(CAST({d.SearchColumn} AS VARCHAR(512)), '')) LIKE @Search";
        var args = new { Search = $"%{search?.Trim().ToUpperInvariant()}%", Skip = (page - 1) * pageSize, Take = pageSize };
        var total = await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {d.Table} {where}", args, cancellationToken: ct));
        var rows = (await c.QueryAsync(new CommandDefinition($"SELECT * FROM {d.Table} {where} ORDER BY {d.SearchColumn} ROWS @Skip + 1 TO @Skip + @Take", args, cancellationToken: ct))).ToList();
        return new(rows, total, page, pageSize);
    }

    private static async Task<PagedResult<dynamic>> ListProcedimentos(DbConnection c, int page, int pageSize, string? search, CancellationToken ct)
    {
        const string from = @"FROM PROCEDIMENTO p
            LEFT JOIN TABELAPROCEDIMENTO tp ON tp.CODTABELAPROCEDIMENTO = p.CODTABELAPROCEDIMENTO
            LEFT JOIN ESPECIALIDADE e ON e.CODESPECIALIDADE = p.CODESPECIALIDADE";
        var where = string.IsNullOrWhiteSpace(search) ? "" : @"WHERE
            UPPER(COALESCE(CAST(p.DESCRICAO_PROCED AS VARCHAR(512)), '')) LIKE @Search OR
            UPPER(COALESCE(CAST(tp.CODIGOTUSS AS VARCHAR(32)), '')) LIKE @Search OR
            UPPER(COALESCE(CAST(e.DESCRICAO AS VARCHAR(512)), '')) LIKE @Search";
        var args = new { Search = $"%{search?.Trim().ToUpperInvariant()}%", Skip = (page - 1) * pageSize, Take = pageSize };
        var total = await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) {from} {where}", args, cancellationToken: ct));
        var rows = (await c.QueryAsync(new CommandDefinition($@"SELECT
                p.*, tp.CODIGOTUSS, tp.DESCRICAOTUSS, e.DESCRICAO AS ESPECIALIDADE
            {from} {where}
            ORDER BY p.DESCRICAO_PROCED
            ROWS @Skip + 1 TO @Skip + @Take", args, cancellationToken: ct))).ToList();
        return new(rows, total, page, pageSize);
    }

    private static async Task<PagedResult<dynamic>> ListScripts(DbConnection c, int page, int pageSize, string? search, int? tipoScript, int? status, int? especialidade, CancellationToken ct)
    {
        const string from = @"FROM SCRIPTLAUDO s
            LEFT JOIN (
                SELECT se.CODSCRIPTLAUDO,
                       LIST(e.DESCRICAO, ', ') AS ESPECIALIDADES
                FROM SCRIPTLAUDO_ESPECIALIDADE se
                JOIN ESPECIALIDADE e ON e.CODESPECIALIDADE = se.CODESPECIALIDADE
                GROUP BY se.CODSCRIPTLAUDO
            ) esp ON esp.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO";
        var where = ScriptFilterConditions(search, tipoScript, status);
        if (especialidade.HasValue && especialidade.Value > 0)
            where.Add(@"EXISTS (
                SELECT 1 FROM SCRIPTLAUDO_ESPECIALIDADE se_filter
                WHERE se_filter.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
                  AND se_filter.CODESPECIALIDADE = @Especialidade
            )");
        else if (especialidade == 0)
            where.Add(@"NOT EXISTS (
                SELECT 1 FROM SCRIPTLAUDO_ESPECIALIDADE se_filter
                WHERE se_filter.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
            )");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var args = new { Search = $"%{search?.Trim().ToUpperInvariant()}%", TipoScript = tipoScript, Status = status, Especialidade = especialidade, Skip = (page - 1) * pageSize, Take = pageSize };
        var total = await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) {from} {whereSql}", args, cancellationToken: ct));
        var rows = (await c.QueryAsync(new CommandDefinition($@"SELECT s.*, esp.ESPECIALIDADES
            {from} {whereSql}
            ORDER BY s.TITULO
            ROWS @Skip + 1 TO @Skip + @Take", args, cancellationToken: ct))).ToList();
        return new(rows, total, page, pageSize);
    }

    private static List<string> ScriptFilterConditions(string? search, int? tipoScript, int? status)
    {
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            where.Add(@"(
                UPPER(COALESCE(CAST(s.TITULO AS VARCHAR(512)), '')) LIKE @Search OR
                UPPER(COALESCE(CAST(s.CODSCRIPTLAUDO AS VARCHAR(32)), '')) LIKE @Search OR
                EXISTS (
                    SELECT 1
                    FROM SCRIPTLAUDO_ESPECIALIDADE se_search
                    JOIN ESPECIALIDADE e_search ON e_search.CODESPECIALIDADE = se_search.CODESPECIALIDADE
                    WHERE se_search.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
                      AND UPPER(COALESCE(CAST(e_search.DESCRICAO AS VARCHAR(512)), '')) LIKE @Search
                )
            )");
        if (tipoScript is 1 or 2 or 3) where.Add("s.TIPOSCRIPT = @TipoScript");
        if (status is -1 or 0) where.Add("s.STATUS = @Status");
        return where;
    }

    private static async Task<PagedResult<dynamic>> ListFrases(DbConnection c, int page, int pageSize, string? search, int? groupId, CancellationToken ct)
    {
        const string from = @"FROM FRASE f
            LEFT JOIN GRUPO g ON g.CODGRUPO = f.CODGRUPO";
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add(@"(
                UPPER(COALESCE(CAST(f.TITULO AS VARCHAR(512)), '')) LIKE @Search OR
                UPPER(COALESCE(CAST(f.CODIGO AS VARCHAR(64)), '')) LIKE @Search
            )");
        }
        if (groupId.HasValue) where.Add("f.CODGRUPO = @GroupId");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var args = new { Search = $"%{search?.Trim().ToUpperInvariant()}%", GroupId = groupId, Skip = (page - 1) * pageSize, Take = pageSize };
        var total = await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) {from} {whereSql}", args, cancellationToken: ct));
        var rows = (await c.QueryAsync(new CommandDefinition($@"SELECT
                f.*, g.GRUPO AS GRUPO_NOME
            {from} {whereSql}
            ORDER BY f.TITULO
            ROWS @Skip + 1 TO @Skip + @Take", args, cancellationToken: ct))).ToList();
        return new(rows, total, page, pageSize);
    }

    public async Task<dynamic?> Get(string domain, int id, CancellationToken ct)
    {
        var d = Resolve(domain); await using var c = await connections.OpenConnectionAsync(ct);
        var item = await c.QuerySingleOrDefaultAsync(new CommandDefinition($"SELECT * FROM {d.Table} WHERE {d.Key}=@Id", new { Id = id }, cancellationToken: ct));
        if (item is null) return null;
        if (domain.Equals("frases", StringComparison.OrdinalIgnoreCase) && item is IDictionary<string, object> fraseRow && fraseRow.TryGetValue("FRASE", out var fraseValue))
            fraseRow["FRASE"] = RtfAnsiHelper.NormalizeForEditing(fraseValue?.ToString());
        return new { item, links = await ReadLinks(c, domain, id, ct) };
    }

    public async Task<int> Create(string domain, object request, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        try { var id = await Insert(c, tx, domain, request, ct); await SaveAggregateLinks(c, tx, domain, id, request, ct); await tx.CommitAsync(ct); return id; }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<bool> Update(string domain, int id, object request, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        try { var changed = await UpdateRow(c, tx, domain, id, request, ct); if (changed) await SaveAggregateLinks(c, tx, domain, id, request, ct); await tx.CommitAsync(ct); return changed; }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<bool> Delete(string domain, int id, CancellationToken ct)
    {
        var d = Resolve(domain); await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        try
        {
            if (domain.Equals("scripts", StringComparison.OrdinalIgnoreCase))
                await DeleteScriptLinks(c, tx, id, ct);
            else
                foreach (var dependency in d.Dependencies) { var p = dependency.Split(':'); var count = await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {p[0]} WHERE {p[1]}=@Id", new { Id = id }, tx, cancellationToken: ct)); if (count > 0) throw new InvalidOperationException($"O registro possui {count} vínculo(s) em {p[0]}."); }
            var changed = await c.ExecuteAsync(new CommandDefinition($"DELETE FROM {d.Table} WHERE {d.Key}=@Id", new { Id = id }, tx, cancellationToken: ct)) > 0; await tx.CommitAsync(ct); return changed;
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<int> DeleteMany(string domain, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (!domain.Equals("scripts", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A exclusão em lote está disponível apenas para scripts.");

        var scriptIds = ids.Where(id => id > 0).Distinct().ToArray();
        if (scriptIds.Length == 0) throw new InvalidOperationException("Selecione ao menos um script para excluir.");

        await using var c = await connections.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        try
        {
            var existing = await c.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO IN @Ids",
                new { Ids = scriptIds }, tx, cancellationToken: ct));
            if (existing != scriptIds.Length) throw new InvalidOperationException("Um ou mais scripts selecionados não foram encontrados.");

            foreach (var id in scriptIds)
                await DeleteScriptLinks(c, tx, id, ct);

            var deleted = await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM SCRIPTLAUDO WHERE CODSCRIPTLAUDO IN @Ids",
                new { Ids = scriptIds }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return deleted;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> SetStatus(string domain, int id, int status, CancellationToken ct)
    {
        if (status is not (-1 or 0)) throw new InvalidOperationException("Status deve ser -1 (ativo) ou 0 (inativo).");
        var d = Resolve(domain);
        if (d.StatusColumn is null) throw new InvalidOperationException("Este domínio não possui status.");
        await using var c = await connections.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        try
        {
            var modificationSql = domain.Equals("scripts", StringComparison.OrdinalIgnoreCase)
                ? ", DATAMODIFICACAO=CURRENT_TIMESTAMP"
                : "";
            var changed = await c.ExecuteAsync(new CommandDefinition(
                $"UPDATE {d.Table} SET {d.StatusColumn}=@Status{modificationSql} WHERE {d.Key}=@Id",
                new { Id = id, Status = status },
                tx,
                cancellationToken: ct)) > 0;
            if (changed && status == 0 && domain.Equals("scripts", StringComparison.OrdinalIgnoreCase))
                await c.ExecuteAsync(new CommandDefinition(@"
                    UPDATE PAGFOTOS
                    SET STATUS = 0
                    WHERE CODPAGFOTOS IN (
                        SELECT CODPAGFOTOS
                        FROM SCRIPTLAUDO_PAGFOTOS
                        WHERE CODSCRIPTLAUDO = @Id
                    )",
                    new { Id = id },
                    tx,
                    cancellationToken: ct));
            await tx.CommitAsync(ct);
            return changed;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task SetLinks(string domain, int id, string relation, IReadOnlyList<int> ids, CancellationToken ct)
    {
        var link = ResolveLink(domain, relation); await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        try { await ReplaceLinks(c, tx, link, id, ids, ct); await TouchScript(c, tx, domain, id, ct); await tx.CommitAsync(ct); } catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task SetSequencedLinks(string domain, int id, string relation, IReadOnlyList<SequencedLink> links, CancellationToken ct)
    {
        var link = ResolveLink(domain, relation); if (link.SequenceColumn is null) throw new InvalidOperationException("O vínculo não possui sequência.");
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        try { await c.ExecuteAsync(new CommandDefinition($"DELETE FROM {link.Table} WHERE {link.ParentColumn}=@Id", new { Id = id }, tx, cancellationToken: ct)); foreach (var x in links.GroupBy(x => x.Id).Select(x => x.First())) await c.ExecuteAsync(new CommandDefinition($"INSERT INTO {link.Table} ({link.ParentColumn},{link.ChildColumn},{link.SequenceColumn}) VALUES (@Id,@Child,@Sequence)", new { Id = id, Child = x.Id, Sequence = x.Sequencia }, tx, cancellationToken: ct)); await TouchScript(c, tx, domain, id, ct); await tx.CommitAsync(ct); }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    private static async Task<int> Insert(DbConnection c, DbTransaction tx, string domain, object request, CancellationToken ct) => domain.ToLowerInvariant() switch
    {
        "especialidades" => await Returning(c, tx, "INSERT INTO ESPECIALIDADE (CODESPECIALIDADE,DESCRICAO) SELECT COALESCE(MAX(CODESPECIALIDADE),0)+1,@Descricao FROM ESPECIALIDADE RETURNING CODESPECIALIDADE", request, ct),
        "grupos" => await Returning(c, tx, "INSERT INTO GRUPO (GRUPO,CODGRUPOPAI,STATUS) VALUES (@Grupo,@CodGrupoPai,@Status) RETURNING CODGRUPO", request, ct),
        "frases" => await Returning(c, tx, "INSERT INTO FRASE (CODGRUPO,CODIGO,TITULO,FRASE,STATUS) VALUES (@CodGrupo,@Codigo,@Titulo,@Frase,@Status) RETURNING CODFRASE", NormalizeFraseRequest(request), ct),
        "operadoras" => await Returning(c, tx, "INSERT INTO OPERADORA (RAZAOSOCIAL,NOMEFANTASIA,UCASE_NOMEFANTASIA,REGISTROANS,CNPJ) VALUES (@RazaoSocial,@NomeFantasia,UPPER(@NomeFantasia),@RegistroAns,@Cnpj) RETURNING CODOPERADORA", request, ct),
        "grupos-operadoras" => await Returning(c, tx, "INSERT INTO GRUPOOPERADORA (DESCRICAO) VALUES (@Descricao) RETURNING CODGRUPOOPERADORA", request, ct),
        "tabela-procedimentos" => await Returning(c, tx, "INSERT INTO TABELAPROCEDIMENTO (CODTABELAPROCEDIMENTO,CODIGOTUSS,DESCRICAOTUSS) SELECT COALESCE(MAX(CODTABELAPROCEDIMENTO),0)+1,@CodigoTuss,@DescricaoTuss FROM TABELAPROCEDIMENTO RETURNING CODTABELAPROCEDIMENTO", request, ct),
        "procedimentos" => await Returning(c, tx, "INSERT INTO PROCEDIMENTO (CODTABELAPROCEDIMENTO,CODESPECIALIDADE,DESCRICAO_PROCED,STATUS) VALUES (@CodTabelaProcedimento,@CodEspecialidade,@Descricao,@Status) RETURNING CODPROCEDIMENTO", request, ct),
        "referencias" => await Returning(c, tx, "INSERT INTO REFERENCIA (DESCRICAO,TIPO,VALOR) VALUES (@Descricao,@Tipo,@Valor) RETURNING CODREFERENCIA", request, ct),
        "esquemas" => await Returning(c, tx, "INSERT INTO ESQUEMAS (CODESQUEMA,DESCRICAO,IMAGEM) SELECT COALESCE(MAX(CODESQUEMA),0)+1,@Descricao,@Imagem FROM ESQUEMAS RETURNING CODESQUEMA", request, ct),
        "esquemas-fotos" => await Returning(c, tx, "INSERT INTO ESQUEMAFOTOS (CODESQUEMAFOTOS,TITULO,ESQUEMA) SELECT COALESCE(MAX(CODESQUEMAFOTOS),0)+1,@Titulo,@Esquema FROM ESQUEMAFOTOS RETURNING CODESQUEMAFOTOS", request, ct),
        "scripts" => await Returning(c, tx, "INSERT INTO SCRIPTLAUDO (TITULO,ESTRUTURASCRIPT,TIPOSCRIPT,STATUS,DATAMODIFICACAO) VALUES (@Titulo,@EstruturaScript,@TipoScript,@Status,CURRENT_TIMESTAMP) RETURNING CODSCRIPTLAUDO", request, ct),
        "paginas-fotos" => await Returning(c, tx, "INSERT INTO PAGFOTOS (TITULO,ESTRUTURAPAGFOTOS,STATUS) VALUES (@Titulo,@EstruturaPagFotos,@Status) RETURNING CODPAGFOTOS", request, ct),
        _ => throw new ArgumentException("Domínio inválido.")
    };

    private static async Task<bool> UpdateRow(DbConnection c, DbTransaction tx, string domain, int id, object request, CancellationToken ct)
    {
        var sql = domain.ToLowerInvariant() switch
        {
            "especialidades" => "UPDATE ESPECIALIDADE SET DESCRICAO=@Descricao WHERE CODESPECIALIDADE=@Id",
            "grupos" => "UPDATE GRUPO SET GRUPO=@Grupo,CODGRUPOPAI=@CodGrupoPai,STATUS=@Status WHERE CODGRUPO=@Id",
            "frases" => "UPDATE FRASE SET CODGRUPO=@CodGrupo,CODIGO=@Codigo,TITULO=@Titulo,FRASE=@Frase,STATUS=@Status WHERE CODFRASE=@Id",
            "operadoras" => "UPDATE OPERADORA SET RAZAOSOCIAL=@RazaoSocial,NOMEFANTASIA=@NomeFantasia,UCASE_NOMEFANTASIA=UPPER(@NomeFantasia),REGISTROANS=@RegistroAns,CNPJ=@Cnpj WHERE CODOPERADORA=@Id",
            "grupos-operadoras" => "UPDATE GRUPOOPERADORA SET DESCRICAO=@Descricao WHERE CODGRUPOOPERADORA=@Id",
            "tabela-procedimentos" => "UPDATE TABELAPROCEDIMENTO SET CODIGOTUSS=@CodigoTuss,DESCRICAOTUSS=@DescricaoTuss,DTHRULTMODIFICACAO=CURRENT_TIMESTAMP WHERE CODTABELAPROCEDIMENTO=@Id",
            "procedimentos" => "UPDATE PROCEDIMENTO SET CODTABELAPROCEDIMENTO=@CodTabelaProcedimento,CODESPECIALIDADE=@CodEspecialidade,DESCRICAO_PROCED=@Descricao,STATUS=@Status WHERE CODPROCEDIMENTO=@Id",
            "referencias" => "UPDATE REFERENCIA SET DESCRICAO=@Descricao,TIPO=@Tipo,VALOR=@Valor WHERE CODREFERENCIA=@Id",
            "esquemas" => "UPDATE ESQUEMAS SET DESCRICAO=@Descricao,IMAGEM=@Imagem WHERE CODESQUEMA=@Id",
            "esquemas-fotos" => "UPDATE ESQUEMAFOTOS SET TITULO=@Titulo,ESQUEMA=@Esquema WHERE CODESQUEMAFOTOS=@Id",
            "scripts" => "UPDATE SCRIPTLAUDO SET TITULO=@Titulo,TIPOSCRIPT=@TipoScript,STATUS=@Status,DATAMODIFICACAO=CURRENT_TIMESTAMP WHERE CODSCRIPTLAUDO=@Id",
            "paginas-fotos" => "UPDATE PAGFOTOS SET TITULO=@Titulo,ESTRUTURAPAGFOTOS=@EstruturaPagFotos,STATUS=@Status WHERE CODPAGFOTOS=@Id",
            _ => throw new ArgumentException("Domínio inválido.")
        };
        var p = new DynamicParameters(domain.Equals("frases", StringComparison.OrdinalIgnoreCase) ? NormalizeFraseRequest(request) : request); p.Add("Id", id); return await c.ExecuteAsync(new CommandDefinition(sql, p, tx, cancellationToken: ct)) > 0;
    }

    private static async Task SaveAggregateLinks(DbConnection c, DbTransaction tx, string domain, int id, object request, CancellationToken ct)
    {
        switch (request)
        {
            case GrupoRequest r when r.Especialidades is not null: await ReplaceLinks(c, tx, ResolveLink(domain, "especialidades"), id, r.Especialidades, ct); break;
            case GrupoOperadoraRequest r: await ReplaceLinks(c, tx, ResolveLink(domain, "operadoras"), id, r.Operadoras ?? [], ct); break;
            case ProcedimentoRequest r: await ReplaceLinks(c, tx, ResolveLink(domain, "frases"), id, r.Frases ?? [], ct); await ReplaceLinks(c, tx, ResolveLink(domain, "grupos"), id, r.Grupos ?? [], ct); await ReplaceLinks(c, tx, ResolveLink(domain, "scripts"), id, r.Scripts ?? [], ct); break;
            case ReferenciaRequest r: await ReplaceLinks(c, tx, ResolveLink(domain, "especialidades"), id, r.Especialidades ?? [], ct); break;
            case EsquemaFotosRequest r: await ReplaceLinks(c, tx, ResolveLink(domain, "especialidades"), id, r.Especialidades ?? [], ct); break;
            case ScriptRequest r:
                if (r.Especialidades is not null) await ReplaceLinks(c, tx, ResolveLink(domain, "especialidades"), id, r.Especialidades, ct);
                if (r.Esquemas is not null) await ReplaceLinks(c, tx, ResolveLink(domain, "esquemas"), id, r.Esquemas, ct);
                if (r.Procedimentos is not null) await ReplaceLinks(c, tx, ResolveLink(domain, "procedimentos"), id, r.Procedimentos, ct);
                if (r.PaginasFotos is not null) await ReplaceSequenced(c, tx, ResolveLink(domain, "paginas-fotos"), id, r.PaginasFotos, ct);
                break;
            case PaginaFotosRequest r: await ReplaceLinks(c, tx, ResolveLink(domain, "especialidades"), id, r.Especialidades ?? [], ct); await ReplaceLinks(c, tx, ResolveLink(domain, "esquemas"), id, r.Esquemas ?? [], ct); await ReplaceSequenced(c, tx, ResolveLink(domain, "scripts"), id, r.Scripts ?? [], ct); break;
        }
    }

    private static FraseRequest NormalizeFraseRequest(object request)
    {
        if (request is not FraseRequest frase) return (FraseRequest)request;
        return frase with { Frase = RtfAnsiHelper.NormalizeForStorage(frase.Frase) };
    }

    private static async Task<object> ReadLinks(DbConnection c, string domain, int id, CancellationToken ct)
    {
        var result = new Dictionary<string, object>();
        foreach (var pair in Links.Where(x => x.Key.StartsWith(domain + ":", StringComparison.OrdinalIgnoreCase)))
        {
            var name = pair.Key[(domain.Length + 1)..]; var l = pair.Value;
            var select = l.SequenceColumn is null ? l.ChildColumn : $"{l.ChildColumn} AS Id,{l.SequenceColumn} AS Sequencia";
            result[name] = (await c.QueryAsync(new CommandDefinition($"SELECT {select} FROM {l.Table} WHERE {l.ParentColumn}=@Id", new { Id = id }, cancellationToken: ct))).ToList();
        }
        return result;
    }

    private static async Task ReplaceLinks(DbConnection c, DbTransaction tx, Link l, int id, IEnumerable<int> ids, CancellationToken ct)
    { await c.ExecuteAsync(new CommandDefinition($"DELETE FROM {l.Table} WHERE {l.ParentColumn}=@Id", new { Id = id }, tx, cancellationToken: ct)); foreach (var child in ids.Distinct()) await c.ExecuteAsync(new CommandDefinition($"INSERT INTO {l.Table} ({l.ParentColumn},{l.ChildColumn}) VALUES (@Id,@Child)", new { Id = id, Child = child }, tx, cancellationToken: ct)); }
    private static async Task DeleteScriptLinks(DbConnection c, DbTransaction tx, int id, CancellationToken ct)
    {
        var args = new { Id = id };
        await c.ExecuteAsync(new CommandDefinition("DELETE FROM SCRIPTLAUDO_ESPECIALIDADE WHERE CODSCRIPTLAUDO=@Id", args, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("DELETE FROM SCRIPTLAUDO_ESQUEMA WHERE CODSCRIPTLAUDO=@Id", args, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("DELETE FROM SCRIPTLAUDO_PAGFOTOS WHERE CODSCRIPTLAUDO=@Id", args, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition("DELETE FROM PROCEDIMENTO_SCRIPTLAUDO WHERE CODSCRIPTLAUDO=@Id", args, tx, cancellationToken: ct));
    }
    private static async Task ReplaceSequenced(DbConnection c, DbTransaction tx, Link l, int id, IEnumerable<SequencedLink> links, CancellationToken ct)
    { await c.ExecuteAsync(new CommandDefinition($"DELETE FROM {l.Table} WHERE {l.ParentColumn}=@Id", new { Id = id }, tx, cancellationToken: ct)); foreach (var x in links.GroupBy(x => x.Id).Select(x => x.First())) await c.ExecuteAsync(new CommandDefinition($"INSERT INTO {l.Table} ({l.ParentColumn},{l.ChildColumn},{l.SequenceColumn}) VALUES (@Id,@Child,@Sequence)", new { Id = id, Child = x.Id, Sequence = x.Sequencia }, tx, cancellationToken: ct)); }
    private static Task<int> Returning(DbConnection c, DbTransaction tx, string sql, object args, CancellationToken ct) => c.ExecuteScalarAsync<int>(new CommandDefinition(sql, args, tx, cancellationToken: ct));
    private static Task TouchScript(DbConnection c, DbTransaction tx, string domain, int id, CancellationToken ct) =>
        domain.Equals("scripts", StringComparison.OrdinalIgnoreCase)
            ? c.ExecuteAsync(new CommandDefinition("UPDATE SCRIPTLAUDO SET DATAMODIFICACAO=CURRENT_TIMESTAMP WHERE CODSCRIPTLAUDO=@Id", new { Id = id }, tx, cancellationToken: ct))
            : Task.CompletedTask;
    private static Domain Resolve(string domain) => Domains.TryGetValue(domain, out var d) ? d : throw new ArgumentException("Domínio inválido.");
    private static Link ResolveLink(string domain, string relation) => Links.TryGetValue($"{domain}:{relation}", out var l) ? l : throw new ArgumentException("Vínculo inválido.");
    private sealed record Domain(string Table, string Key, string SearchColumn, string? StatusColumn, string[] Dependencies);
    private sealed record Link(string Table, string ParentColumn, string ChildColumn, string? SequenceColumn = null);
    private static readonly IReadOnlyDictionary<string, Link> Links = new Dictionary<string, Link>(StringComparer.OrdinalIgnoreCase)
    {
        ["grupos:especialidades"] = new("GRUPO_ESPECIALIDADE","CODGRUPO","CODESPECIALIDADE"), ["grupos-operadoras:operadoras"] = new("GRUPOOPERADORA_OPERADORA","CODGRUPOOPERADORA","CODOPERADORA"),
        ["procedimentos:frases"] = new("PROCEDIMENTO_FRASE","CODPROCEDIMENTO","CODFRASE"), ["procedimentos:grupos"] = new("PROCEDIMENTO_GRUPO","CODPROCEDIMENTO","CODGRUPO"), ["procedimentos:scripts"] = new("PROCEDIMENTO_SCRIPTLAUDO","CODPROCEDIMENTO","CODSCRIPTLAUDO"),
        ["referencias:especialidades"] = new("REFERENCIA_ESPECIALIDADE","CODREFERENCIA","CODESPECIALIDADE"), ["esquemas-fotos:especialidades"] = new("ESQUEMAFOTOS_ESPECIALIDADE","CODESQUEMAFOTOS","CODESPECIALIDADE"),
        ["scripts:especialidades"] = new("SCRIPTLAUDO_ESPECIALIDADE","CODSCRIPTLAUDO","CODESPECIALIDADE"), ["scripts:esquemas"] = new("SCRIPTLAUDO_ESQUEMA","CODSCRIPTLAUDO","CODESQUEMA"), ["scripts:procedimentos"] = new("PROCEDIMENTO_SCRIPTLAUDO","CODSCRIPTLAUDO","CODPROCEDIMENTO"), ["scripts:paginas-fotos"] = new("SCRIPTLAUDO_PAGFOTOS","CODSCRIPTLAUDO","CODPAGFOTOS","SEQUENCIA"),
        ["paginas-fotos:especialidades"] = new("PAGFOTOS_ESPECIALIDADE","CODPAGFOTOS","CODESPECIALIDADE"), ["paginas-fotos:esquemas"] = new("PAGFOTOS_ESQUEMA","CODPAGFOTOS","CODESQUEMA"), ["paginas-fotos:scripts"] = new("SCRIPTLAUDO_PAGFOTOS","CODPAGFOTOS","CODSCRIPTLAUDO","SEQUENCIA")
    };
}
