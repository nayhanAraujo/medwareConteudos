using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IVariaveisWebService
{
    Task<object> ListVariaveisAsync(int skip, int take, string? search, int? grupo, CancellationToken ct);
    Task<object> ListGruposAsync(CancellationToken ct);
    Task<object?> GetVariavelAsync(int id, CancellationToken ct);
}

public class VariaveisWebService : IVariaveisWebService
{
    private readonly IFirebirdConnectionFactory _db;
    public VariaveisWebService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<object> ListGruposAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<GrupoVariavelDto>(@"
            SELECT CODGRUPO AS CodGrupo, NOME AS Nome
            FROM GRUPOS_VARIAVEIS
            WHERE ATIVO = 1
            ORDER BY NOME");
        return ApiResponse.Ok(rows);
    }

    public async Task<object> ListVariaveisAsync(int skip, int take, string? search, int? grupo, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        var where = new List<string> { "1=1" };
        var parameters = new DynamicParameters();
        parameters.Add("take", take);
        parameters.Add("skip", skip);

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(UPPER(V.NOME) LIKE UPPER(@search) OR UPPER(V.VARIAVEL) LIKE UPPER(@search) OR UPPER(G.NOME) LIKE UPPER(@search))");
            parameters.Add("search", $"%{search.Trim()}%");
        }

        if (grupo.HasValue)
        {
            where.Add("V.CODGRUPO = @grupo");
            parameters.Add("grupo", grupo.Value);
        }

        var whereClause = string.Join(" AND ", where);

        var countSql = $@"
            SELECT COUNT(*)
            FROM VARIAVEIS V
            LEFT JOIN GRUPOS_VARIAVEIS G ON V.CODGRUPO = G.CODGRUPO
            WHERE {whereClause}";

        var total = await conn.ExecuteScalarAsync<int>(countSql, parameters);

        var sql = $@"
            SELECT FIRST @take SKIP @skip
                   V.CODVARIAVEL AS CodVariavel,
                   V.NOME AS Nome,
                   V.VARIAVEL AS Variavel,
                   V.SIGLA AS Sigla,
                   V.ABREVIACAO AS Abreviacao,
                   V.CODGRUPO AS CodGrupo,
                   G.NOME AS NomeGrupo
            FROM VARIAVEIS V
            LEFT JOIN GRUPOS_VARIAVEIS G ON V.CODGRUPO = G.CODGRUPO
            WHERE {whereClause}
            ORDER BY COALESCE(G.NOME, ''), V.NOME";

        var baseRows = (await conn.QueryAsync<VariavelListRow>(sql, parameters)).ToList();
        var items = new List<VariavelListItem>();

        foreach (var row in baseRows)
        {
            var cod = row.CodVariavel;

            var formulaRow = await conn.QueryFirstOrDefaultAsync<FormulaRow>(@"
                SELECT f.FORMULA AS Formula, f.CASADECIMAIS AS CasasDecimais
                FROM FORMULAS f
                JOIN FORMULA_VARIAVEL fv ON f.CODFORMULA = fv.CODFORMULA
                WHERE fv.CODVARIAVEL = @cod", new { cod });

            var alternativas = (await conn.QueryAsync<string>(@"
                SELECT ALTERNATIVA FROM VARIAVEIS_ALTERNATIVAS WHERE CODVARIAVEL = @cod", new { cod }))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

            var scripts = (await conn.QueryAsync<VariavelScriptDto>(@"
                SELECT s.CODSCRIPTLAUDO AS CodScriptLaudo, s.NOME AS Nome
                FROM SCRIPTLAUDO_VARIAVEL sv
                JOIN SCRIPTLAUDO s ON sv.CODSCRIPTLAUDO = s.CODSCRIPTLAUDO
                WHERE sv.CODVARIAVEL = @cod", new { cod })).ToList();

            var anexoRows = await conn.QueryAsync<AnexoRow>(@"
                SELECT a.CODANEXO AS CodAnexo,
                       a.TIPO_ANEXO AS TipoAnexo,
                       a.CAMINHO AS Caminho,
                       a.LINK AS Link,
                       a.DESCRICAO AS Descricao,
                       r.TITULO AS RefTitulo,
                       r.ANO AS RefAno,
                       LIST(aut.NOME, ', ') AS RefAutores
                FROM ANEXO_VARIAVEL_FORMULA avf
                JOIN ANEXOS a ON avf.COD_ANEXO = a.CODANEXO
                LEFT JOIN REFERENCIA r ON avf.COD_REFERENCIA = r.CODREFERENCIA
                LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
                LEFT JOIN AUTORES aut ON ra.CODAUTOR = aut.CODAUTOR
                WHERE avf.CODVARIAVEL = @cod
                GROUP BY a.CODANEXO, a.TIPO_ANEXO, a.CAMINHO, a.LINK, a.DESCRICAO, r.TITULO, r.ANO", new { cod });

            var anexos = anexoRows.Select(a => new VariavelAnexoDto(
                a.CodAnexo,
                a.TipoAnexo,
                !string.IsNullOrWhiteSpace(a.Link) ? a.Link : a.Caminho,
                a.Descricao,
                string.IsNullOrWhiteSpace(a.RefTitulo)
                    ? null
                    : new VariavelAnexoReferenciaDto(a.RefTitulo, a.RefAno, a.RefAutores)
            )).ToList();

            var possuiNormalidades = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM NORMALIDADE WHERE CODVARIAVEL = @cod", new { cod }) > 0;

            items.Add(new VariavelListItem(
                row.CodVariavel,
                row.Nome,
                row.Variavel,
                row.Sigla,
                row.Abreviacao,
                row.CodGrupo,
                row.NomeGrupo,
                formulaRow?.Formula,
                formulaRow?.CasasDecimais,
                alternativas,
                scripts,
                anexos,
                possuiNormalidades));
        }

        return ApiResponse.Ok(items, total);
    }

    public async Task<object?> GetVariavelAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync("SELECT * FROM VARIAVEIS WHERE CODVARIAVEL = @id", new { id });
    }

    private sealed class VariavelListRow
    {
        public int CodVariavel { get; init; }
        public string? Nome { get; init; }
        public string? Variavel { get; init; }
        public string? Sigla { get; init; }
        public string? Abreviacao { get; init; }
        public int? CodGrupo { get; init; }
        public string? NomeGrupo { get; init; }
    }

    private sealed class FormulaRow
    {
        public string? Formula { get; init; }
        public int? CasasDecimais { get; init; }
    }

    private sealed class AnexoRow
    {
        public int CodAnexo { get; init; }
        public string? TipoAnexo { get; init; }
        public string? Caminho { get; init; }
        public string? Link { get; init; }
        public string? Descricao { get; init; }
        public string? RefTitulo { get; init; }
        public string? RefAno { get; init; }
        public string? RefAutores { get; init; }
    }
}
