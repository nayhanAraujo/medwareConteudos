using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IVariaveisWebService
{
    Task<object> ListVariaveisAsync(int skip, int take, string? search, int? grupo, CancellationToken ct);
    Task<object> ListGruposAsync(CancellationToken ct);
    Task<object?> GetVariavelAsync(int id, CancellationToken ct);
    Task<object> GetDetalhesCompletosAsync(int id, CancellationToken ct);
    Task<object> GetCodigosVinculadosAsync(int id, CancellationToken ct);
    Task<object> GetMetaAsync(CancellationToken ct);
    Task<int> CreateVariavelAsync(VariavelCreateRequest req, int codUsuario, CancellationToken ct);
    Task<object?> GetVariavelEdicaoAsync(int id, CancellationToken ct);
    Task UpdateVariavelAsync(int id, VariavelCreateRequest req, int codUsuario, CancellationToken ct);
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

    public async Task<object> GetDetalhesCompletosAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        var normalidadeRows = await conn.QueryAsync<NormalidadeDetalheRow>(@"
            SELECT n.CODNORMALIDADE AS CodNormalidade,
                   n.SEXO AS Sexo,
                   n.VALORMIN AS ValorMin,
                   n.VALORMAX AS ValorMax,
                   n.IDADE_MIN AS IdadeMin,
                   n.IDADE_MAX AS IdadeMax,
                   r.CODREFERENCIA AS RefCodigo,
                   r.TITULO AS RefTitulo,
                   r.ANO AS RefAno,
                   r.DESCRICAO AS RefDescricao,
                   LIST(a.NOME || ' (' || COALESCE(a.ABREVIACAO, '') || ')', ', ') AS RefAutores
            FROM NORMALIDADE n
            LEFT JOIN REFERENCIA r ON n.CODREFERENCIA = r.CODREFERENCIA
            LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
            LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
            WHERE n.CODVARIAVEL = @id
            GROUP BY n.CODNORMALIDADE, n.SEXO, n.VALORMIN, n.VALORMAX,
                     n.IDADE_MIN, n.IDADE_MAX, r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO
            ORDER BY r.ANO DESC NULLS LAST", new { id });

        var equacaoRows = await conn.QueryAsync<EquacaoDetalheRow>(@"
            SELECT el.CODEQUACAO AS CodEquacao,
                   el.EQUACAO AS Equacao,
                   tl.NOME AS Linguagem,
                   r.CODREFERENCIA AS RefCodigo,
                   r.TITULO AS RefTitulo,
                   r.ANO AS RefAno,
                   r.DESCRICAO AS RefDescricao,
                   LIST(a.NOME || ' (' || COALESCE(a.ABREVIACAO, '') || ')', ', ') AS RefAutores
            FROM FORMULA_VARIAVEL fv
            JOIN FORMULAS f ON fv.CODFORMULA = f.CODFORMULA
            JOIN EQUACOES_LINGUAGEM el ON f.CODFORMULA = el.CODFORMULA
            JOIN TIPOLINGUAGEM tl ON el.CODLINGUAGEM = tl.CODLINGUAGEM
            LEFT JOIN REFERENCIA r ON el.CODREFERENCIA = r.CODREFERENCIA
            LEFT JOIN REFERENCIA_AUTORES ra ON r.CODREFERENCIA = ra.CODREFERENCIA
            LEFT JOIN AUTORES a ON ra.CODAUTOR = a.CODAUTOR
            WHERE fv.CODVARIAVEL = @id
            GROUP BY el.CODEQUACAO, el.EQUACAO, tl.NOME,
                     r.CODREFERENCIA, r.TITULO, r.ANO, r.DESCRICAO
            ORDER BY r.ANO DESC NULLS LAST", new { id });

        var normalidades = normalidadeRows.Select(n => new VariavelNormalidadeDetalheDto(
            n.CodNormalidade,
            n.Sexo,
            n.ValorMin,
            n.ValorMax,
            n.IdadeMin,
            n.IdadeMax,
            n.RefCodigo.HasValue
                ? new VariavelReferenciaResumoDto(n.RefCodigo, n.RefTitulo, n.RefAno, n.RefDescricao, n.RefAutores)
                : null)).ToList();

        var equacoes = equacaoRows.Select(e => new VariavelEquacaoDetalheDto(
            e.CodEquacao,
            e.Equacao,
            e.Linguagem,
            e.RefCodigo.HasValue
                ? new VariavelReferenciaResumoDto(e.RefCodigo, e.RefTitulo, e.RefAno, e.RefDescricao, e.RefAutores)
                : null)).ToList();

        return ApiResponse.Ok(new VariavelDetalhesCompletosDto(normalidades, equacoes));
    }

    public async Task<object> GetCodigosVinculadosAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<VariavelCodigoDicomDto>(@"
            SELECT cu.CODIGO AS Codigo, cu.DESCRICAOPTBR AS DescricaoPtBr
            FROM VARIAVEL_CODIGO_UNIVERSAL vcu
            JOIN CODIGO_UNIVERSAL cu ON vcu.COD_UNIVERSAL = cu.COD_UNIVERSAL
            WHERE vcu.CODVARIAVEL = @id
            ORDER BY cu.CODIGO", new { id });
        return ApiResponse.Ok(rows);
    }

    public async Task<object> GetMetaAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var unidades = await conn.QueryAsync<UnidadeMedidaDto>(@"
            SELECT CODUNIDADEMEDIDA AS CodUnidadeMedida, DESCRICAO AS Descricao
            FROM UNIDADEMEDIDA
            ORDER BY DESCRICAO");
        return ApiResponse.Ok(new { unidades });
    }

    public async Task<int> CreateVariavelAsync(VariavelCreateRequest req, int codUsuario, CancellationToken ct)
    {
        var nome = req.Nome?.Trim() ?? "";
        var variavel = req.Variavel?.Trim() ?? "";
        var sigla = req.Sigla?.Trim().ToUpperInvariant() ?? "";
        var abreviacao = req.Abreviacao?.Trim() ?? "";
        var descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();

        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("O campo 'Nome Clínico' é obrigatório.");
        if (string.IsNullOrWhiteSpace(variavel))
            throw new InvalidOperationException("O campo 'Variável' é obrigatório.");
        if (string.IsNullOrWhiteSpace(sigla))
            throw new InvalidOperationException("O campo 'Sigla' é obrigatório.");
        if (string.IsNullOrWhiteSpace(abreviacao))
            throw new InvalidOperationException("O campo 'Abreviação' é obrigatório.");
        if (req.CodUnidadeMedida <= 0)
            throw new InvalidOperationException("O campo 'Unidade de Medida' é obrigatório.");
        if (req.CasasDecimais < 0)
            throw new InvalidOperationException("Casas decimais inválidas.");
        if (!variavel.StartsWith("VR_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O campo 'Variável' deve começar com 'VR_'.");

        await using var conn = await _db.OpenConnectionAsync(ct);

        var variavelExiste = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE UPPER(VARIAVEL) = UPPER(@variavel)",
            new { variavel });
        if (variavelExiste > 0)
            throw new InvalidOperationException("Já existe uma variável com esse código.");

        var siglaExiste = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE UPPER(SIGLA) = UPPER(@sigla)",
            new { sigla });
        if (siglaExiste > 0)
            throw new InvalidOperationException("Já existe uma variável com essa sigla.");

        var codVariavel = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO VARIAVEIS (
                NOME, VARIAVEL, SIGLA, ABREVIACAO, DESCRICAO,
                CODUNIDADEMEDIDA, CASASDECIMAIS, CODUSUARIO, DTHRULTMODIFICACAO
            )
            VALUES (
                @nome, @variavel, @sigla, @abreviacao, @descricao,
                @codUnidadeMedida, @casasDecimais, @codUsuario, @now
            )
            RETURNING CODVARIAVEL",
            new
            {
                nome,
                variavel,
                sigla,
                abreviacao,
                descricao,
                codUnidadeMedida = req.CodUnidadeMedida,
                casasDecimais = req.CasasDecimais,
                codUsuario,
                now = DateTime.Now
            });

        foreach (var alternativa in req.Alternativas ?? Array.Empty<string>())
        {
            var alt = alternativa?.Trim();
            if (string.IsNullOrWhiteSpace(alt)) continue;
            await conn.ExecuteAsync(@"
                INSERT INTO VARIAVEIS_ALTERNATIVAS (CODVARIAVEL, ALTERNATIVA, CODUSUARIO, DTHRULTMODIFICACAO)
                VALUES (@codVariavel, @alt, @codUsuario, @now)",
                new { codVariavel, alt, codUsuario, now = DateTime.Now });
        }

        return codVariavel;
    }

    public async Task<object?> GetVariavelEdicaoAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<VariavelEdicaoRow>(@"
            SELECT CODVARIAVEL AS CodVariavel,
                   NOME AS Nome,
                   VARIAVEL AS Variavel,
                   SIGLA AS Sigla,
                   ABREVIACAO AS Abreviacao,
                   DESCRICAO AS Descricao,
                   CODUNIDADEMEDIDA AS CodUnidadeMedida,
                   CASASDECIMAIS AS CasasDecimais
            FROM VARIAVEIS
            WHERE CODVARIAVEL = @id", new { id });

        if (row is null) return null;

        var alternativas = (await conn.QueryAsync<string>(@"
            SELECT ALTERNATIVA FROM VARIAVEIS_ALTERNATIVAS WHERE CODVARIAVEL = @id ORDER BY ALTERNATIVA", new { id }))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        return ApiResponse.Ok(new VariavelEdicaoDto(
            row.CodVariavel,
            row.Nome,
            row.Variavel,
            row.Sigla,
            row.Abreviacao,
            row.Descricao,
            row.CodUnidadeMedida,
            row.CasasDecimais,
            alternativas));
    }

    public async Task UpdateVariavelAsync(int id, VariavelCreateRequest req, int codUsuario, CancellationToken ct)
    {
        var nome = req.Nome?.Trim() ?? "";
        var variavel = req.Variavel?.Trim() ?? "";
        var sigla = req.Sigla?.Trim().ToUpperInvariant() ?? "";
        var abreviacao = req.Abreviacao?.Trim() ?? "";
        var descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();

        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("O campo 'Nome Clínico' é obrigatório.");
        if (string.IsNullOrWhiteSpace(variavel))
            throw new InvalidOperationException("O campo 'Variável' é obrigatório.");
        if (string.IsNullOrWhiteSpace(sigla))
            throw new InvalidOperationException("O campo 'Sigla' é obrigatório.");
        if (string.IsNullOrWhiteSpace(abreviacao))
            throw new InvalidOperationException("O campo 'Abreviação' é obrigatório.");
        if (req.CodUnidadeMedida <= 0)
            throw new InvalidOperationException("O campo 'Unidade de Medida' é obrigatório.");
        if (req.CasasDecimais < 0)
            throw new InvalidOperationException("Casas decimais inválidas.");
        if (!variavel.StartsWith("VR_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O campo 'Variável' deve começar com 'VR_'.");

        await using var conn = await _db.OpenConnectionAsync(ct);

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE CODVARIAVEL = @id", new { id });
        if (exists == 0)
            throw new InvalidOperationException("Variável não encontrada.");

        var variavelExiste = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE UPPER(VARIAVEL) = UPPER(@variavel) AND CODVARIAVEL <> @id",
            new { variavel, id });
        if (variavelExiste > 0)
            throw new InvalidOperationException("Já existe uma variável com esse código.");

        var siglaExiste = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE UPPER(SIGLA) = UPPER(@sigla) AND CODVARIAVEL <> @id",
            new { sigla, id });
        if (siglaExiste > 0)
            throw new InvalidOperationException("Já existe uma variável com essa sigla.");

        await conn.ExecuteAsync(@"
            UPDATE VARIAVEIS
            SET NOME = @nome,
                VARIAVEL = @variavel,
                SIGLA = @sigla,
                ABREVIACAO = @abreviacao,
                DESCRICAO = @descricao,
                CODUNIDADEMEDIDA = @codUnidadeMedida,
                CASASDECIMAIS = @casasDecimais,
                CODUSUARIO = @codUsuario,
                DTHRULTMODIFICACAO = @now
            WHERE CODVARIAVEL = @id",
            new
            {
                nome,
                variavel,
                sigla,
                abreviacao,
                descricao,
                codUnidadeMedida = req.CodUnidadeMedida,
                casasDecimais = req.CasasDecimais,
                codUsuario,
                now = DateTime.Now,
                id
            });

        await conn.ExecuteAsync("DELETE FROM VARIAVEIS_ALTERNATIVAS WHERE CODVARIAVEL = @id", new { id });

        foreach (var alternativa in req.Alternativas ?? Array.Empty<string>())
        {
            var alt = alternativa?.Trim();
            if (string.IsNullOrWhiteSpace(alt)) continue;
            await conn.ExecuteAsync(@"
                INSERT INTO VARIAVEIS_ALTERNATIVAS (CODVARIAVEL, ALTERNATIVA, CODUSUARIO, DTHRULTMODIFICACAO)
                VALUES (@id, @alt, @codUsuario, @now)",
                new { id, alt, codUsuario, now = DateTime.Now });
        }
    }

    private sealed class VariavelEdicaoRow
    {
        public int CodVariavel { get; init; }
        public string? Nome { get; init; }
        public string? Variavel { get; init; }
        public string? Sigla { get; init; }
        public string? Abreviacao { get; init; }
        public string? Descricao { get; init; }
        public int? CodUnidadeMedida { get; init; }
        public int? CasasDecimais { get; init; }
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

    private sealed class NormalidadeDetalheRow
    {
        public int CodNormalidade { get; init; }
        public string? Sexo { get; init; }
        public decimal? ValorMin { get; init; }
        public decimal? ValorMax { get; init; }
        public int? IdadeMin { get; init; }
        public int? IdadeMax { get; init; }
        public int? RefCodigo { get; init; }
        public string? RefTitulo { get; init; }
        public string? RefAno { get; init; }
        public string? RefDescricao { get; init; }
        public string? RefAutores { get; init; }
    }

    private sealed class EquacaoDetalheRow
    {
        public int CodEquacao { get; init; }
        public string? Equacao { get; init; }
        public string? Linguagem { get; init; }
        public int? RefCodigo { get; init; }
        public string? RefTitulo { get; init; }
        public string? RefAno { get; init; }
        public string? RefDescricao { get; init; }
        public string? RefAutores { get; init; }
    }
}
