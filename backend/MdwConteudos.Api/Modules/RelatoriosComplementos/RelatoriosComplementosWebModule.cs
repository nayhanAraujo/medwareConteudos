using System.Data.Common;
using System.Text;
using System.Xml.Linq;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Modules.RelatoriosComplementos;

public static class RelatoriosComplementosRegistration
{
    public static IServiceCollection AddRelatoriosComplementos(this IServiceCollection services)
        => services.AddScoped<IRelatoriosComplementosService, RelatoriosComplementosService>();
}

public sealed record ValidacaoRequest(string StatusValidacao, string MetodoValidacao, string? CriteriosValidacao, string? Observacoes, DateTime? DthrProximaValidacao);
public sealed record FiltroRequest(string Nome, string Descricao, string Tipo, string? SqlFiltro, string SqlQuery, string Ativo = "S");

public interface IRelatoriosComplementosService
{
    Task<object> ListValidacoes(int relatorioId, CancellationToken ct);
    Task CreateValidacao(int relatorioId, ValidacaoRequest request, int usuarioId, CancellationToken ct);
    Task<object> ListFiltros(CancellationToken ct);
    Task<int> CreateFiltro(FiltroRequest request, int usuarioId, CancellationToken ct);
    Task UpdateFiltro(int id, FiltroRequest request, int usuarioId, CancellationToken ct);
    Task DeleteFiltro(int id, CancellationToken ct);
    Task<object> ListColunas(int relatorioId, CancellationToken ct);
    Task<object> SearchColunas(string query, CancellationToken ct);
    Task<object> Reindexar(int usuarioId, CancellationToken ct);
    Task<object> StatusColunas(CancellationToken ct);
}

public sealed class RelatoriosComplementosService(IFirebirdConnectionFactory db) : IRelatoriosComplementosService
{
    public async Task<object> ListValidacoes(int relatorioId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureResource(conn, "RELATORIOS", "CODRELATORIO", relatorioId, "Relatório não encontrado");
        if (!await TableExists(conn, "RELATORIO_VALIDACOES")) return new { data = Array.Empty<object>(), total = 0, tabelaExiste = false };
        var raw = (await conn.QueryAsync(@"SELECT v.CODVALIDACAO, v.STATUS_VALIDACAO, v.METODO_VALIDACAO,
                   v.CRITERIOS_VALIDACAO, v.OBSERVACOES, v.DTHRVALIDACAO, v.DTHRPROXIMA_VALIDACAO,
                   u.NOME AS VALIDADOR FROM RELATORIO_VALIDACOES v
                   LEFT JOIN USUARIO u ON u.CODUSUARIO = v.CODUSUARIO_VALIDADOR
                   WHERE v.CODRELATORIO=@relatorioId ORDER BY v.DTHRVALIDACAO DESC", new { relatorioId })).ToList();
        var rows = raw.Select(v => new
        {
            codvalidacao = Convert.ToInt32(v.CODVALIDACAO),
            status_validacao = Convert.ToString(v.STATUS_VALIDACAO) ?? string.Empty,
            metodo_validacao = Convert.ToString(v.METODO_VALIDACAO) ?? string.Empty,
            criterios_validacao = Convert.ToString(v.CRITERIOS_VALIDACAO),
            observacoes = Convert.ToString(v.OBSERVACOES),
            dthrvalidacao = v.DTHRVALIDACAO,
            dthrproxima_validacao = v.DTHRPROXIMA_VALIDACAO,
            validador = Convert.ToString(v.VALIDADOR) ?? "N/A"
        }).ToList();
        return new { data = rows, total = rows.Count, tabelaExiste = true };
    }

    public async Task CreateValidacao(int relatorioId, ValidacaoRequest req, int usuarioId, CancellationToken ct)
    {
        var status = req.StatusValidacao.Trim().ToUpperInvariant();
        if (status is not ("A" or "R" or "P")) throw new InvalidOperationException("Status deve ser A, R ou P.");
        if (string.IsNullOrWhiteSpace(req.MetodoValidacao)) throw new InvalidOperationException("Método de validação é obrigatório.");
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureResource(conn, "RELATORIOS", "CODRELATORIO", relatorioId, "Relatório não encontrado");
        if (!await TableExists(conn, "RELATORIO_VALIDACOES")) throw new InvalidOperationException("Tabela RELATORIO_VALIDACOES não existe.");
        await conn.ExecuteAsync(@"INSERT INTO RELATORIO_VALIDACOES
            (CODRELATORIO,CODUSUARIO_VALIDADOR,STATUS_VALIDACAO,METODO_VALIDACAO,CRITERIOS_VALIDACAO,OBSERVACOES,DTHRPROXIMA_VALIDACAO)
            VALUES (@relatorioId,@usuarioId,@status,@metodo,@criterios,@observacoes,@proxima)",
            new { relatorioId, usuarioId, status, metodo = req.MetodoValidacao.Trim(), criterios = req.CriteriosValidacao, observacoes = req.Observacoes, proxima = req.DthrProximaValidacao });
    }

    public async Task<object> ListFiltros(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        if (!await TableExists(conn, "RELATORIO_FILTROS")) return new { data = Array.Empty<object>(), total = 0, tabelaExiste = false };
        var raw = (await conn.QueryAsync(@"SELECT CODFILTRO,NOME,DESCRICAO,TIPO,SQL_FILTRO,SQL_QUERY,ATIVO,DTHRCRIACAO,DTHRATUALIZACAO
            FROM RELATORIO_FILTROS ORDER BY NOME")).ToList();
        var rows = raw.Select(f => new
        {
            codfiltro = Convert.ToInt32(f.CODFILTRO), nome = Convert.ToString(f.NOME) ?? string.Empty,
            descricao = Convert.ToString(f.DESCRICAO) ?? string.Empty, tipo = Convert.ToString(f.TIPO) ?? string.Empty,
            sql_filtro = Convert.ToString(f.SQL_FILTRO), sql_query = Convert.ToString(f.SQL_QUERY) ?? string.Empty,
            ativo = Convert.ToString(f.ATIVO) ?? "S", dthrcriacao = f.DTHRCRIACAO, dthratualizacao = f.DTHRATUALIZACAO
        }).ToList();
        return new { data = rows, total = rows.Count, tabelaExiste = true };
    }

    public async Task<int> CreateFiltro(FiltroRequest req, int usuarioId, CancellationToken ct)
    {
        ValidateFiltro(req);
        await using var conn = await db.OpenConnectionAsync(ct);
        if (!await TableExists(conn, "RELATORIO_FILTROS")) throw new InvalidOperationException("Tabela RELATORIO_FILTROS não existe.");
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RELATORIO_FILTROS WHERE UPPER(NOME)=UPPER(@nome)", new { req.Nome }) > 0)
            throw new InvalidOperationException("Já existe um filtro com este nome.");
        return await conn.ExecuteScalarAsync<int>(@"INSERT INTO RELATORIO_FILTROS
            (NOME,DESCRICAO,TIPO,SQL_FILTRO,SQL_QUERY,ATIVO,USUARIOCRIACAO)
            VALUES (@Nome,@Descricao,@Tipo,@SqlFiltro,@SqlQuery,@Ativo,@usuarioId) RETURNING CODFILTRO", new { req.Nome, req.Descricao, Tipo = req.Tipo.ToUpperInvariant(), req.SqlFiltro, req.SqlQuery, req.Ativo, usuarioId });
    }

    public async Task UpdateFiltro(int id, FiltroRequest req, int usuarioId, CancellationToken ct)
    {
        ValidateFiltro(req);
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureResource(conn, "RELATORIO_FILTROS", "CODFILTRO", id, "Filtro não encontrado");
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RELATORIO_FILTROS WHERE UPPER(NOME)=UPPER(@nome) AND CODFILTRO<>@id", new { req.Nome, id }) > 0)
            throw new InvalidOperationException("Já existe um filtro com este nome.");
        await conn.ExecuteAsync(@"UPDATE RELATORIO_FILTROS SET NOME=@Nome,DESCRICAO=@Descricao,TIPO=@Tipo,
            SQL_FILTRO=@SqlFiltro,SQL_QUERY=@SqlQuery,ATIVO=@Ativo,USUARIOATUALIZACAO=@usuarioId WHERE CODFILTRO=@id",
            new { req.Nome, req.Descricao, Tipo = req.Tipo.ToUpperInvariant(), req.SqlFiltro, req.SqlQuery, req.Ativo, usuarioId, id });
    }

    public async Task DeleteFiltro(int id, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        if (await conn.ExecuteAsync("DELETE FROM RELATORIO_FILTROS WHERE CODFILTRO=@id", new { id }) == 0)
            throw new KeyNotFoundException("Filtro não encontrado.");
    }

    public async Task<object> ListColunas(int relatorioId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureResource(conn, "RELATORIOS", "CODRELATORIO", relatorioId, "Relatório não encontrado");
        if (!await TableExists(conn, "RELATORIO_COLUNAS")) return new { data = Array.Empty<object>(), total = 0, tabelaExiste = false };
        var raw = (await conn.QueryAsync(@"SELECT NOME_COLUNA,POSICAO_COLUNA,TIPO_COLUNA,DTHRCRIACAO
            FROM RELATORIO_COLUNAS WHERE CODRELATORIO=@relatorioId AND ATIVO=1 ORDER BY POSICAO_COLUNA", new { relatorioId })).ToList();
        var rows = raw.Select(c => new
        {
            nome_coluna = Convert.ToString(c.NOME_COLUNA) ?? string.Empty,
            posicao_coluna = Convert.ToInt32(c.POSICAO_COLUNA),
            tipo_coluna = Convert.ToString(c.TIPO_COLUNA) ?? "TEXTO",
            dthrcriacao = c.DTHRCRIACAO
        }).ToList();
        return new { data = rows, total = rows.Count, tabelaExiste = true };
    }

    public async Task<object> SearchColunas(string query, CancellationToken ct)
    {
        var terms = query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0) throw new InvalidOperationException("Informe ao menos uma coluna.");
        await using var conn = await db.OpenConnectionAsync(ct);
        if (!await TableExists(conn, "RELATORIO_COLUNAS")) throw new InvalidOperationException("Tabela RELATORIO_COLUNAS não existe.");
        var p = new DynamicParameters(); var clauses = new List<string>();
        for (var i = 0; i < terms.Length; i++) { clauses.Add($"UPPER(rc.NOME_COLUNA) LIKE UPPER(@term{i})"); p.Add($"term{i}", $"%{terms[i]}%"); }
        var rows = (await conn.QueryAsync($@"SELECT r.CODRELATORIO,r.NOME,r.MODULO,r.FORMATO,r.DTHRCRIACAO,r.ATIVO,rc.NOME_COLUNA,rc.POSICAO_COLUNA
            FROM RELATORIOS r JOIN RELATORIO_COLUNAS rc ON rc.CODRELATORIO=r.CODRELATORIO
            WHERE ({string.Join(" OR ", clauses)}) AND r.ATIVO=1 AND rc.ATIVO=1 ORDER BY r.NOME,rc.POSICAO_COLUNA", p)).ToList();
        var grouped = rows.GroupBy(r => Convert.ToInt32(r.CODRELATORIO)).Where(g => terms.All(t => g.Any(r => Convert.ToString(r.NOME_COLUNA)?.Contains(t, StringComparison.OrdinalIgnoreCase) == true))).Select(g => new
        {
            codRelatorio = g.Key, nome = Convert.ToString(g.First().NOME) ?? string.Empty, modulo = Convert.ToString(g.First().MODULO), formato = Convert.ToString(g.First().FORMATO),
            dthrCriacao = g.First().DTHRCRIACAO, ativo = Convert.ToInt32(g.First().ATIVO),
            colunasEncontradas = g.Select(r => new { nome = Convert.ToString(r.NOME_COLUNA) ?? string.Empty, posicao = r.POSICAO_COLUNA is null ? null : (int?)Convert.ToInt32(r.POSICAO_COLUNA) }).ToList()
        }).ToList();
        return new { data = grouped, total = grouped.Count, colunaBuscada = query, colunasProcessadas = terms, tipoBusca = terms.Length > 1 ? "múltipla" : "única" };
    }

    public async Task<object> Reindexar(int usuarioId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        if (!await TableExists(conn, "RELATORIO_COLUNAS")) throw new InvalidOperationException("Tabela RELATORIO_COLUNAS não existe.");
        var relatorios = (await conn.QueryAsync("SELECT CODRELATORIO,NOME,CONTEUDO FROM RELATORIOS WHERE FORMATO='XML' AND ATIVO=1")).ToList();
        var resultados = new List<object>(); var sucessos = 0;
        foreach (var rel in relatorios)
        {
            try
            {
                var conteudo = DecodeContent(rel.CONTEUDO); var cols = ExtractColumns(conteudo);
                using var tx = conn.BeginTransaction();
                var relatorioId = Convert.ToInt32(rel.CODRELATORIO);
                await conn.ExecuteAsync("DELETE FROM RELATORIO_COLUNAS WHERE CODRELATORIO=@id", new { id = relatorioId }, tx);
                foreach (var col in cols) await conn.ExecuteAsync(@"INSERT INTO RELATORIO_COLUNAS
                    (CODRELATORIO,NOME_COLUNA,POSICAO_COLUNA,TIPO_COLUNA,ATIVO,USUARIOCRIACAO)
                    VALUES (@id,@nome,@posicao,'TEXTO',1,@usuarioId)", new { id = relatorioId, nome = col.Name, posicao = col.Position, usuarioId }, tx);
                tx.Commit(); sucessos++; resultados.Add(new { codRelatorio = relatorioId, nome = Convert.ToString(rel.NOME) ?? string.Empty, sucesso = true, colunas = cols.Count });
            }
            catch (Exception ex) { resultados.Add(new { codRelatorio = Convert.ToInt32(rel.CODRELATORIO), nome = Convert.ToString(rel.NOME) ?? string.Empty, sucesso = false, erro = ex.Message }); }
        }
        return new { message = $"Reindexação concluída: {sucessos} sucessos, {relatorios.Count - sucessos} falhas", totalRelatorios = relatorios.Count, sucessos, falhas = relatorios.Count - sucessos, resultados };
    }

    public async Task<object> StatusColunas(CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        if (!await TableExists(conn, "RELATORIO_COLUNAS")) return new { tabelaExiste = false, message = "Tabela RELATORIO_COLUNAS não existe. Execute o script SQL primeiro." };
        var stats = await conn.QuerySingleAsync(@"SELECT COUNT(DISTINCT r.CODRELATORIO) TOTAL_RELATORIOS_XML,
            COUNT(rc.CODCOLUNA) TOTAL_COLUNAS_INDEXADAS,COUNT(DISTINCT rc.CODRELATORIO) RELATORIOS_COM_COLUNAS
            FROM RELATORIOS r LEFT JOIN RELATORIO_COLUNAS rc ON rc.CODRELATORIO=r.CODRELATORIO WHERE r.FORMATO='XML' AND r.ATIVO=1");
        var missingRaw = (await conn.QueryAsync(@"SELECT r.CODRELATORIO,r.NOME,r.DTHRCRIACAO FROM RELATORIOS r
            LEFT JOIN RELATORIO_COLUNAS rc ON rc.CODRELATORIO=r.CODRELATORIO
            WHERE r.FORMATO='XML' AND r.ATIVO=1 AND rc.CODRELATORIO IS NULL ORDER BY r.DTHRCRIACAO DESC")).ToList();
        var missing = missingRaw.Select(r => new { codrelatorio = Convert.ToInt32(r.CODRELATORIO), nome = Convert.ToString(r.NOME) ?? string.Empty, dthrcriacao = r.DTHRCRIACAO }).ToList();
        return new
        {
            tabelaExiste = true,
            totalRelatoriosXml = Convert.ToInt32(stats.TOTAL_RELATORIOS_XML),
            totalColunasIndexadas = Convert.ToInt32(stats.TOTAL_COLUNAS_INDEXADAS),
            relatoriosComColunas = Convert.ToInt32(stats.RELATORIOS_COM_COLUNAS),
            relatoriosSemColunas = missing.Count,
            relatoriosSemColunasLista = missing
        };
    }

    private static void ValidateFiltro(FiltroRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Descricao) || string.IsNullOrWhiteSpace(req.Tipo) || string.IsNullOrWhiteSpace(req.SqlQuery))
            throw new InvalidOperationException("Campos obrigatórios não preenchidos.");
        if (req.Tipo.ToUpperInvariant() is "COMUM" or "MULTISELECAO" && string.IsNullOrWhiteSpace(req.SqlFiltro))
            throw new InvalidOperationException("SQL do filtro é obrigatório.");
    }

    private static async Task<bool> TableExists(DbConnection conn, string table)
        => await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME=@table", new { table = table.ToUpperInvariant() }) > 0;
    private static async Task EnsureResource(DbConnection conn, string table, string key, int id, string message)
    { if (await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE {key}=@id", new { id }) == 0) throw new KeyNotFoundException(message); }
    private static string DecodeContent(object value) => value switch { byte[] b => Encoding.UTF8.GetString(b), _ => Convert.ToString(value) ?? string.Empty };
    private static List<(string Name, int Position)> ExtractColumns(string xml)
    {
        var doc = XDocument.Parse(xml); var i = 0;
        return doc.Descendants().Where(x => x.Name.LocalName == "COL")
            .Select(x => x.Elements().FirstOrDefault(e => e.Name.LocalName == "TEXT")?.Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => (x!, ++i)).ToList();
    }
}

[ApiController]
[Route("api/web/relatorios")]
[Authorize]
public sealed class RelatoriosComplementosController(IRelatoriosComplementosService service) : ControllerBase
{
    [HttpGet("{id:int}/validacoes")] public async Task<IActionResult> Validacoes(int id, CancellationToken ct) => Ok(ApiResponse.Ok(await service.ListValidacoes(id, ct)));
    [HttpPost("{id:int}/validacoes")][Authorize(Roles="admin")] public async Task<IActionResult> CriarValidacao(int id, ValidacaoRequest req, CancellationToken ct) => await Execute(async () => { await service.CreateValidacao(id, req, User.GetCodUsuario(), ct); return ApiResponse.OkMessage("Validação cadastrada."); });
    [HttpGet("filtros")] public async Task<IActionResult> Filtros(CancellationToken ct) => Ok(ApiResponse.Ok(await service.ListFiltros(ct)));
    [HttpPost("filtros")][Authorize(Roles="admin")] public async Task<IActionResult> CriarFiltro(FiltroRequest req, CancellationToken ct) => await Execute(async () => ApiResponse.Ok(new { codFiltro = await service.CreateFiltro(req, User.GetCodUsuario(), ct) }));
    [HttpPut("filtros/{id:int}")][Authorize(Roles="admin")] public async Task<IActionResult> EditarFiltro(int id, FiltroRequest req, CancellationToken ct) => await Execute(async () => { await service.UpdateFiltro(id, req, User.GetCodUsuario(), ct); return ApiResponse.OkMessage("Filtro atualizado."); });
    [HttpDelete("filtros/{id:int}")][Authorize(Roles="admin")] public async Task<IActionResult> ExcluirFiltro(int id, CancellationToken ct) => await Execute(async () => { await service.DeleteFiltro(id, ct); return ApiResponse.OkMessage("Filtro excluído."); });
    [HttpGet("{id:int}/colunas")] public async Task<IActionResult> Colunas(int id, CancellationToken ct) => Ok(ApiResponse.Ok(await service.ListColunas(id, ct)));
    [HttpGet("colunas/search")] public async Task<IActionResult> Buscar([FromQuery]string q, CancellationToken ct) => await Execute(async () => ApiResponse.Ok(await service.SearchColunas(q, ct)));
    [HttpPost("colunas/reindexar")][Authorize(Roles="admin")] public async Task<IActionResult> Reindexar(CancellationToken ct) => await Execute(async () => ApiResponse.Ok(await service.Reindexar(User.GetCodUsuario(), ct)));
    [HttpGet("colunas/status")] public async Task<IActionResult> Status(CancellationToken ct) => Ok(ApiResponse.Ok(await service.StatusColunas(ct)));

    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }
}
