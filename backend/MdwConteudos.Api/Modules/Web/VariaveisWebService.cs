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
    Task<VariavelDependenciasDto?> GetDependenciasAsync(int id, CancellationToken ct);
    Task DeleteVariavelAsync(int id, bool force, CancellationToken ct);
    Task AlterarGrupoAsync(int id, int? codGrupo, int codUsuario, CancellationToken ct);
    Task<object> ListClassificacoesAsync(CancellationToken ct);
    Task<int> CreateGrupoClassificacaoAsync(string nome, int codUsuario, CancellationToken ct);
    Task UpdateGrupoClassificacaoAsync(int id, string nome, int codUsuario, CancellationToken ct);
    Task DeleteGrupoClassificacaoAsync(int id, CancellationToken ct);
    Task<int> CreateClassificacaoAsync(ClassificacaoRequest req, int codUsuario, CancellationToken ct);
    Task UpdateClassificacaoAsync(int id, ClassificacaoRequest req, int codUsuario, CancellationToken ct);
    Task DeleteClassificacaoAsync(int id, CancellationToken ct);
    Task<object> GetVariavelClassificacoesAsync(int id, CancellationToken ct);
    Task SetVariavelClassificacoesAsync(int id, IReadOnlyList<int> classificacoes, int codUsuario, CancellationToken ct);
    Task<object> ListCodigosUniversaisAsync(string? search, CancellationToken ct);
    Task SetCodigosUniversaisAsync(int id, IReadOnlyList<int> codigos, int codUsuario, CancellationToken ct);
    Task<object> GetEspecialidadesAsync(int id, CancellationToken ct);
    Task SetEspecialidadeAsync(int id, EspecialidadeVinculoRequest req, int codUsuario, CancellationToken ct);
    Task RemoveEspecialidadeAsync(int id, int codEspecialidade, CancellationToken ct);
    Task SetEspecialidadesLoteAsync(EspecialidadesLoteRequest req, int codUsuario, CancellationToken ct);
    Task<object> GetAnexosContextoAsync(int id, CancellationToken ct);
    Task<int> CreateAnexoAsync(int id, string tipo, string? nome, string? descricao, string? link, string? caminho, int? codFormula, int? codReferencia, int codUsuario, CancellationToken ct);
    Task DeleteAnexoAsync(int id, int codAnexo, CancellationToken ct);
    Task<object> GetEstudosAsync(int id, CancellationToken ct);
    Task<object> ListModelosModoTextoAsync(string? search, int codUsuario, CancellationToken ct);
    Task<(string Nome, string Conteudo)?> GerarModoTextoAsync(int id, int codUsuario, CancellationToken ct);
    Task<ImportacaoCsPreview> PreviewImportacaoAsync(string source, CancellationToken ct);
    Task<object> ImportarCsAsync(ImportacaoCsConfirmarRequest req, int codUsuario, CancellationToken ct);
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
                   CASASDECIMAIS AS CasasDecimais,
                   CODGRUPO AS CodGrupo
            FROM VARIAVEIS
            WHERE CODVARIAVEL = @id", new { id });

        if (row is null) return null;

        var alternativas = (await conn.QueryAsync<string>(@"
            SELECT ALTERNATIVA FROM VARIAVEIS_ALTERNATIVAS WHERE CODVARIAVEL = @id ORDER BY ALTERNATIVA", new { id }))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        return ApiResponse.Ok(new {
            row.CodVariavel, row.Nome, row.Variavel, row.Sigla, row.Abreviacao,
            row.Descricao, row.CodUnidadeMedida, row.CasasDecimais, row.CodGrupo,
            Alternativas = alternativas
        });
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

    public async Task<VariavelDependenciasDto?> GetDependenciasAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS WHERE CODVARIAVEL=@id", new { id }) == 0) return null;
        async Task<int> Count(string table, string column) =>
            await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE {column}=@id", new { id });
        return new VariavelDependenciasDto(
            await Count("FORMULA_VARIAVEL", "CODVARIAVEL"),
            await Count("NORMALIDADE", "CODVARIAVEL"),
            await Count("SCRIPTLAUDO_VARIAVEL", "CODVARIAVEL"),
            await Count("SECAO_VARIAVEL", "CODVARIAVEL"),
            await Count("VARIAVEL_CODIGO_UNIVERSAL", "CODVARIAVEL"),
            await Count("VARIAVEIS_ALTERNATIVAS", "CODVARIAVEL"),
            await Count("VARIAVEIS_CLASSIFICACOES", "CODVARIAVEL"),
            await Count("VARIAVEL_ESPECIALIDADE", "CODVARIAVEL"),
            await Count("ANEXO_VARIAVEL_FORMULA", "CODVARIAVEL"));
    }

    public async Task DeleteVariavelAsync(int id, bool force, CancellationToken ct)
    {
        var deps = await GetDependenciasAsync(id, ct) ?? throw new KeyNotFoundException("Variável não encontrada.");
        if (deps.PossuiVinculos && !force)
            throw new InvalidOperationException("A variável possui vínculos. Confirme a exclusão forçada para remover os vínculos transacionalmente.");

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var formulas = (await conn.QueryAsync<int>("SELECT CODFORMULA FROM FORMULA_VARIAVEL WHERE CODVARIAVEL=@id", new { id }, tx)).ToList();
            var anexos = (await conn.QueryAsync<int>("SELECT COD_ANEXO FROM ANEXO_VARIAVEL_FORMULA WHERE CODVARIAVEL=@id", new { id }, tx)).ToList();
            foreach (var table in new[] { "NORMALIDADE", "SCRIPTLAUDO_VARIAVEL", "SECAO_VARIAVEL", "VARIAVEL_CODIGO_UNIVERSAL", "VARIAVEIS_ALTERNATIVAS", "VARIAVEIS_CLASSIFICACOES", "VARIAVEL_ESPECIALIDADE", "ANEXO_VARIAVEL_FORMULA" })
                await conn.ExecuteAsync($"DELETE FROM {table} WHERE CODVARIAVEL=@id", new { id }, tx);
            await conn.ExecuteAsync("DELETE FROM FORMULA_VARIAVEL WHERE CODVARIAVEL=@id", new { id }, tx);
            foreach (var formula in formulas)
            {
                if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM FORMULA_VARIAVEL WHERE CODFORMULA=@formula", new { formula }, tx) > 0) continue;
                await conn.ExecuteAsync("DELETE FROM EQUACOES_LINGUAGEM WHERE CODFORMULA=@formula", new { formula }, tx);
                await conn.ExecuteAsync("DELETE FROM FORMULAS WHERE CODFORMULA=@formula", new { formula }, tx);
            }
            foreach (var anexo in anexos)
            {
                if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ANEXO_VARIAVEL_FORMULA WHERE COD_ANEXO=@anexo", new { anexo }, tx) == 0)
                    await conn.ExecuteAsync("DELETE FROM ANEXOS WHERE CODANEXO=@anexo", new { anexo }, tx);
            }
            var rows = await conn.ExecuteAsync("DELETE FROM VARIAVEIS WHERE CODVARIAVEL=@id", new { id }, tx);
            if (rows == 0) throw new KeyNotFoundException("Variável não encontrada.");
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task AlterarGrupoAsync(int id, int? codGrupo, int codUsuario, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (codGrupo.HasValue && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRUPOS_VARIAVEIS WHERE CODGRUPO=@codGrupo", new { codGrupo }) == 0)
            throw new InvalidOperationException("Grupo não encontrado.");
        var rows = await conn.ExecuteAsync(@"UPDATE VARIAVEIS SET CODGRUPO=@codGrupo,CODUSUARIO=@codUsuario,DTHRULTMODIFICACAO=@now WHERE CODVARIAVEL=@id",
            new { id, codGrupo, codUsuario, now = DateTime.Now });
        if (rows == 0) throw new KeyNotFoundException("Variável não encontrada.");
    }

    public async Task<object> ListClassificacoesAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var groups = (await conn.QueryAsync<ClassificacaoGrupoDto>("SELECT CODGRUPO AS CodGrupo,NOME AS Nome FROM GRUPOS_CLASSIFICACOES ORDER BY NOME")).ToList();
        var classes = (await conn.QueryAsync<ClassificacaoDto>("SELECT CODCLASSIFICACAO AS CodClassificacao,CODGRUPO AS CodGrupo,NOME AS Nome FROM CLASSIFICACOES ORDER BY NOME")).ToList();
        return ApiResponse.Ok(new { grupos = groups, classificacoes = classes });
    }

    public Task<int> CreateGrupoClassificacaoAsync(string nome, int codUsuario, CancellationToken ct) => CreateNamedAsync("GRUPOS_CLASSIFICACOES", "CODGRUPO", nome, null, codUsuario, ct);
    public Task UpdateGrupoClassificacaoAsync(int id, string nome, int codUsuario, CancellationToken ct) => UpdateNamedAsync("GRUPOS_CLASSIFICACOES", "CODGRUPO", id, nome, null, codUsuario, ct);
    public async Task DeleteGrupoClassificacaoAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLASSIFICACOES WHERE CODGRUPO=@id", new { id }) > 0)
            throw new InvalidOperationException("Não é possível excluir o grupo porque ele possui classificações associadas.");
        if (await conn.ExecuteAsync("DELETE FROM GRUPOS_CLASSIFICACOES WHERE CODGRUPO=@id", new { id }) == 0) throw new KeyNotFoundException("Grupo não encontrado.");
    }
    public Task<int> CreateClassificacaoAsync(ClassificacaoRequest req, int codUsuario, CancellationToken ct) => CreateNamedAsync("CLASSIFICACOES", "CODCLASSIFICACAO", req.Nome, req.CodGrupo, codUsuario, ct);
    public Task UpdateClassificacaoAsync(int id, ClassificacaoRequest req, int codUsuario, CancellationToken ct) => UpdateNamedAsync("CLASSIFICACOES", "CODCLASSIFICACAO", id, req.Nome, req.CodGrupo, codUsuario, ct);
    public async Task DeleteClassificacaoAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS_CLASSIFICACOES WHERE CODCLASSIFICACAO=@id", new { id }) > 0 ||
            await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM NORMALIDADE WHERE CODCLASSIFICACAO=@id", new { id }) > 0)
            throw new InvalidOperationException("Não é possível excluir uma classificação vinculada a variáveis ou normalidades.");
        if (await conn.ExecuteAsync("DELETE FROM CLASSIFICACOES WHERE CODCLASSIFICACAO=@id", new { id }) == 0) throw new KeyNotFoundException("Classificação não encontrada.");
    }

    public async Task<object> GetVariavelClassificacoesAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ClassificacaoDto>(@"SELECT c.CODCLASSIFICACAO AS CodClassificacao,c.CODGRUPO AS CodGrupo,c.NOME AS Nome FROM VARIAVEIS_CLASSIFICACOES vc JOIN CLASSIFICACOES c ON c.CODCLASSIFICACAO=vc.CODCLASSIFICACAO WHERE vc.CODVARIAVEL=@id ORDER BY c.NOME", new { id });
        return ApiResponse.Ok(rows);
    }

    public async Task SetVariavelClassificacoesAsync(int id, IReadOnlyList<int> classificacoes, int codUsuario, CancellationToken ct)
    {
        var ids = classificacoes.Distinct().ToList();
        await using var conn = await _db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS WHERE CODVARIAVEL=@id", new { id }, tx) == 0) throw new KeyNotFoundException("Variável não encontrada.");
            if (ids.Count > 0 && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CLASSIFICACOES WHERE CODCLASSIFICACAO IN @ids", new { ids }, tx) != ids.Count) throw new InvalidOperationException("Uma ou mais classificações são inválidas.");
            await conn.ExecuteAsync("DELETE FROM VARIAVEIS_CLASSIFICACOES WHERE CODVARIAVEL=@id", new { id }, tx);
            foreach (var code in ids) await conn.ExecuteAsync("INSERT INTO VARIAVEIS_CLASSIFICACOES (CODVARIAVEL,CODCLASSIFICACAO,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@id,@code,@codUsuario,@now)", new { id, code, codUsuario, now = DateTime.Now }, tx);
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    private async Task<int> CreateNamedAsync(string table, string key, string rawName, int? group, int user, CancellationToken ct)
    {
        var name = Required(rawName, "Nome");
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE UPPER(NOME)=UPPER(@name)", new { name }) > 0) throw new InvalidOperationException("Já existe um registro com esse nome.");
        if (group.HasValue && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GRUPOS_CLASSIFICACOES WHERE CODGRUPO=@group", new { group }) == 0) throw new InvalidOperationException("Grupo não encontrado.");
        var groupColumns = group.HasValue ? ",CODGRUPO" : "";
        var groupValues = group.HasValue ? ",@group" : "";
        return await conn.ExecuteScalarAsync<int>($"INSERT INTO {table} (NOME{groupColumns},CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@name{groupValues},@user,@now) RETURNING {key}", new { name, group, user, now = DateTime.Now });
    }

    private async Task UpdateNamedAsync(string table, string key, int id, string rawName, int? group, int user, CancellationToken ct)
    {
        var name = Required(rawName, "Nome");
        await using var conn = await _db.OpenConnectionAsync(ct);
        if (await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE UPPER(NOME)=UPPER(@name) AND {key}<>@id", new { name, id }) > 0) throw new InvalidOperationException("Já existe um registro com esse nome.");
        var groupSet = group.HasValue ? ",CODGRUPO=@group" : "";
        var rows = await conn.ExecuteAsync($"UPDATE {table} SET NOME=@name{groupSet},CODUSUARIO=@user,DTHRULTMODIFICACAO=@now WHERE {key}=@id", new { name, group, user, now = DateTime.Now, id });
        if (rows == 0) throw new KeyNotFoundException("Registro não encontrado.");
    }

    public async Task<object> ListCodigosUniversaisAsync(string? search, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var q = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        var rows = await conn.QueryAsync<CodigoUniversalComplementoDto>(@"SELECT FIRST 500 COD_UNIVERSAL AS CodUniversal,CODIGO AS Codigo,DESCRICAOPTBR AS DescricaoPtBr FROM CODIGO_UNIVERSAL WHERE @q IS NULL OR UPPER(CODIGO) LIKE UPPER(@q) OR UPPER(DESCRICAOPTBR) LIKE UPPER(@q) ORDER BY CODIGO", new { q });
        return ApiResponse.Ok(rows);
    }

    public async Task SetCodigosUniversaisAsync(int id, IReadOnlyList<int> codigos, int codUsuario, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS WHERE CODVARIAVEL=@id", new { id }, tx) == 0) throw new KeyNotFoundException("Variável não encontrada.");
            var ids = codigos.Distinct().ToList();
            if (ids.Count > 0 && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CODIGO_UNIVERSAL WHERE COD_UNIVERSAL IN @ids", new { ids }, tx) != ids.Count) throw new InvalidOperationException("Um ou mais códigos universais são inválidos.");
            await conn.ExecuteAsync("DELETE FROM VARIAVEL_CODIGO_UNIVERSAL WHERE CODVARIAVEL=@id", new { id }, tx);
            foreach (var code in ids) await conn.ExecuteAsync("INSERT INTO VARIAVEL_CODIGO_UNIVERSAL (CODVARIAVEL,COD_UNIVERSAL,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@id,@code,@codUsuario,@now)", new { id, code, codUsuario, now = DateTime.Now }, tx);
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<object> GetEspecialidadesAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var vinculadas = await conn.QueryAsync<EspecialidadeComplementoDto>(@"SELECT ve.CODESPECIALIDADE AS CodEspecialidade,e.NOME AS Nome,ve.DESCRICAO AS Descricao FROM VARIAVEL_ESPECIALIDADE ve JOIN ESPECIALIDADE e ON e.CODESPECIALIDADE=ve.CODESPECIALIDADE WHERE ve.CODVARIAVEL=@id ORDER BY e.NOME", new { id });
        var disponiveis = await conn.QueryAsync<EspecialidadeComplementoDto>(@"SELECT CODESPECIALIDADE AS CodEspecialidade,NOME AS Nome,CAST(NULL AS VARCHAR(1000)) AS Descricao FROM ESPECIALIDADE WHERE CODESPECIALIDADE NOT IN (SELECT CODESPECIALIDADE FROM VARIAVEL_ESPECIALIDADE WHERE CODVARIAVEL=@id) ORDER BY NOME", new { id });
        return ApiResponse.Ok(new { vinculadas, disponiveis });
    }

    public async Task SetEspecialidadeAsync(int id, EspecialidadeVinculoRequest req, int codUsuario, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await conn.ExecuteAsync("UPDATE OR INSERT INTO VARIAVEL_ESPECIALIDADE (CODVARIAVEL,CODESPECIALIDADE,DESCRICAO,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@id,@CodEspecialidade,@Descricao,@codUsuario,@now) MATCHING (CODVARIAVEL,CODESPECIALIDADE)", new { id, req.CodEspecialidade, req.Descricao, codUsuario, now = DateTime.Now });
    }
    public async Task RemoveEspecialidadeAsync(int id, int codEspecialidade, CancellationToken ct) { await using var conn = await _db.OpenConnectionAsync(ct); await conn.ExecuteAsync("DELETE FROM VARIAVEL_ESPECIALIDADE WHERE CODVARIAVEL=@id AND CODESPECIALIDADE=@codEspecialidade", new { id, codEspecialidade }); }
    public async Task SetEspecialidadesLoteAsync(EspecialidadesLoteRequest req, int codUsuario, CancellationToken ct)
    {
        var ids = req.Variaveis.Distinct().ToList(); if (ids.Count == 0) throw new InvalidOperationException("Selecione ao menos uma variável.");
        await using var conn = await _db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(ct);
        try { foreach (var id in ids) await conn.ExecuteAsync("UPDATE OR INSERT INTO VARIAVEL_ESPECIALIDADE (CODVARIAVEL,CODESPECIALIDADE,DESCRICAO,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@id,@CodEspecialidade,@Descricao,@codUsuario,@now) MATCHING (CODVARIAVEL,CODESPECIALIDADE)", new { id, req.CodEspecialidade, req.Descricao, codUsuario, now = DateTime.Now }, tx); await tx.CommitAsync(ct); }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<object> GetAnexosContextoAsync(int id, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var anexos = await conn.QueryAsync<AnexoComplementoDto>(@"SELECT a.CODANEXO AS CodAnexo,a.NOME AS Nome,a.DESCRICAO AS Descricao,a.TIPO_ANEXO AS TipoAnexo,a.LINK AS Link,a.CAMINHO AS Caminho,avf.CODFORMULA AS CodFormula,avf.COD_REFERENCIA AS CodReferencia FROM ANEXO_VARIAVEL_FORMULA avf JOIN ANEXOS a ON a.CODANEXO=avf.COD_ANEXO WHERE avf.CODVARIAVEL=@id ORDER BY a.CODANEXO DESC", new { id });
        var formulas = await conn.QueryAsync<FormulaOpcaoDto>(@"SELECT f.CODFORMULA AS CodFormula,f.FORMULA AS Formula FROM FORMULA_VARIAVEL fv JOIN FORMULAS f ON f.CODFORMULA=fv.CODFORMULA WHERE fv.CODVARIAVEL=@id", new { id });
        var referencias = await conn.QueryAsync<ReferenciaOpcaoDto>("SELECT CODREFERENCIA AS CodReferencia,TITULO AS Titulo,ANO AS Ano FROM REFERENCIA ORDER BY ANO DESC,TITULO");
        return ApiResponse.Ok(new { anexos, formulas, referencias });
    }

    public async Task<int> CreateAnexoAsync(int id, string tipo, string? nome, string? descricao, string? link, string? caminho, int? codFormula, int? codReferencia, int codUsuario, CancellationToken ct)
    {
        tipo = Required(tipo, "Tipo").ToUpperInvariant(); if (tipo == "URL" && string.IsNullOrWhiteSpace(link)) throw new InvalidOperationException("A URL é obrigatória."); if (tipo != "URL" && string.IsNullOrWhiteSpace(caminho)) throw new InvalidOperationException("O arquivo é obrigatório.");
        await using var conn = await _db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(ct);
        try { var code = await conn.ExecuteScalarAsync<int>("INSERT INTO ANEXOS (CODREFERENCIA,DESCRICAO,NOME,LINK,CAMINHO,CODUSUARIO,DTHRULTMODIFICACAO,TIPO_ANEXO) VALUES (@codReferencia,@descricao,@nome,@link,@caminho,@codUsuario,@now,@tipo) RETURNING CODANEXO", new { codReferencia, descricao, nome, link, caminho, codUsuario, now = DateTime.Now, tipo }, tx); await conn.ExecuteAsync("INSERT INTO ANEXO_VARIAVEL_FORMULA (COD_ANEXO,CODVARIAVEL,CODFORMULA,COD_REFERENCIA) VALUES (@code,@id,@codFormula,@codReferencia)", new { code, id, codFormula, codReferencia }, tx); await tx.CommitAsync(ct); return code; }
        catch { await tx.RollbackAsync(ct); throw; }
    }
    public async Task DeleteAnexoAsync(int id, int codAnexo, CancellationToken ct) { await using var conn = await _db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(ct); try { await conn.ExecuteAsync("DELETE FROM ANEXO_VARIAVEL_FORMULA WHERE CODVARIAVEL=@id AND COD_ANEXO=@codAnexo", new { id, codAnexo }, tx); if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ANEXO_VARIAVEL_FORMULA WHERE COD_ANEXO=@codAnexo", new { codAnexo }, tx) == 0) await conn.ExecuteAsync("DELETE FROM ANEXOS WHERE CODANEXO=@codAnexo", new { codAnexo }, tx); await tx.CommitAsync(ct); } catch { await tx.RollbackAsync(ct); throw; } }
    public async Task<object> GetEstudosAsync(int id, CancellationToken ct) { await using var conn = await _db.OpenConnectionAsync(ct); var rows = await conn.QueryAsync<EstudoVariavelDto>(@"SELECT f.CODFORMULA AS CodFormula,f.FORMULA AS Formula,r.TITULO AS TituloReferencia,r.ANO AS AnoReferencia,a2.CODANEXO AS CodAnexo,a2.TIPO_ANEXO AS TipoAnexo,COALESCE(a2.LINK,a2.CAMINHO) AS Caminho,a2.DESCRICAO AS Descricao FROM FORMULA_VARIAVEL fv JOIN FORMULAS f ON f.CODFORMULA=fv.CODFORMULA LEFT JOIN EQUACOES_LINGUAGEM el ON el.CODFORMULA=f.CODFORMULA LEFT JOIN REFERENCIA r ON r.CODREFERENCIA=el.CODREFERENCIA LEFT JOIN ANEXO_VARIAVEL_FORMULA avf ON avf.CODVARIAVEL=fv.CODVARIAVEL AND avf.CODFORMULA=f.CODFORMULA LEFT JOIN ANEXOS a2 ON a2.CODANEXO=avf.COD_ANEXO WHERE fv.CODVARIAVEL=@id", new { id }); return ApiResponse.Ok(rows); }

    public async Task<object> ListModelosModoTextoAsync(string? search, int codUsuario, CancellationToken ct) { await using var conn = await _db.OpenConnectionAsync(ct); var q = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%"; var rows = await conn.QueryAsync<ModeloModoTextoDto>(@"SELECT CODMODELO AS CodModelo,NOME AS Nome,(SELECT COUNT(*) FROM SECAO_MODO_TEXTO s WHERE s.CODMODELO=m.CODMODELO) AS TotalSecoes FROM MODELO_MODO_TEXTO m WHERE CODUSUARIO=@codUsuario AND (@q IS NULL OR UPPER(NOME) LIKE UPPER(@q)) ORDER BY NOME", new { codUsuario, q }); return ApiResponse.Ok(rows); }
    public async Task<(string Nome, string Conteudo)?> GerarModoTextoAsync(int id, int codUsuario, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct); var name = await conn.QueryFirstOrDefaultAsync<string>("SELECT NOME FROM MODELO_MODO_TEXTO WHERE CODMODELO=@id AND CODUSUARIO=@codUsuario", new { id, codUsuario }); if (name is null) return null;
        var sections = await conn.QueryAsync<SecaoModoTextoDto>("SELECT CODSECAO AS CodSecao,NOME AS Nome FROM SECAO_MODO_TEXTO WHERE CODMODELO=@id ORDER BY ORDEM,NOME", new { id }); var lines = new List<string>();
        foreach (var section in sections) { lines.Add($"[{section.Nome}]"); var vars = await conn.QueryAsync<VariavelModoTextoDto>(@"SELECT v.CODVARIAVEL AS CodVariavel,v.SIGLA AS Sigla,u.DESCRICAO AS Unidade,(SELECT FIRST 1 f.FORMULA FROM FORMULA_VARIAVEL fv JOIN FORMULAS f ON f.CODFORMULA=fv.CODFORMULA WHERE fv.CODVARIAVEL=v.CODVARIAVEL) AS Formula FROM SECAO_VARIAVEL sv JOIN VARIAVEIS v ON v.CODVARIAVEL=sv.CODVARIAVEL LEFT JOIN UNIDADEMEDIDA u ON u.CODUNIDADEMEDIDA=v.CODUNIDADEMEDIDA WHERE sv.CODSECAO=@sectionId ORDER BY v.SIGLA", new { sectionId = section.CodSecao }); foreach (var variable in vars) { var norms = await conn.QueryAsync<NormalidadeModoTextoDto>("SELECT SEXO AS Sexo,VALORMIN AS ValorMin,VALORMAX AS ValorMax FROM NORMALIDADE WHERE CODVARIAVEL=@id", new { id = variable.CodVariavel }); var ranges = string.Concat(norms.Select(n => $" ({n.Sexo}: {n.ValorMin} a {n.ValorMax})")); var formula = variable.Formula is null ? "" : $" Código: {variable.Formula}"; lines.Add($"{variable.Sigla}: 0.0 {variable.Unidade ?? "sem unidade"}{ranges}{formula}"); } lines.Add(""); }
        return (name, string.Join(Environment.NewLine, lines));
    }

    public async Task<ImportacaoCsPreview> PreviewImportacaoAsync(string source, CancellationToken ct)
    {
        var parsed = CSharpVariaveisParser.Parse(source); await using var conn = await _db.OpenConnectionAsync(ct); var vars = new List<ImportacaoCsVariavel>(); foreach (var item in parsed.Variaveis) { var exists = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS WHERE UPPER(VARIAVEL)=UPPER(@Codigo)", item) > 0; vars.Add(item with { ExisteNoBanco = exists }); } return parsed with { Variaveis = vars };
    }
    public async Task<object> ImportarCsAsync(ImportacaoCsConfirmarRequest req, int codUsuario, CancellationToken ct)
    {
        var selected = req.VariaveisSelecionadas.ToHashSet(StringComparer.OrdinalIgnoreCase); if (selected.Count == 0) throw new InvalidOperationException("Selecione ao menos uma variável.");
        await using var conn = await _db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(ct); var inserted = 0; var ignored = 0;
        try { foreach (var item in req.Variaveis.Where(v => selected.Contains(v.Codigo))) { if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VARIAVEIS WHERE UPPER(VARIAVEL)=UPPER(@Codigo)", item, tx) > 0) { ignored++; continue; } var unit = await conn.QueryFirstOrDefaultAsync<int?>("SELECT CODUNIDADEMEDIDA FROM UNIDADEMEDIDA WHERE UPPER(DESCRICAO)=UPPER(@Unidade)", item, tx); if (!unit.HasValue) unit = await conn.ExecuteScalarAsync<int>("INSERT INTO UNIDADEMEDIDA (DESCRICAO,STATUS,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@Unidade,-1,@codUsuario,@now) RETURNING CODUNIDADEMEDIDA", new { item.Unidade, codUsuario, now = DateTime.Now }, tx); var varId = await conn.ExecuteScalarAsync<int>("INSERT INTO VARIAVEIS (NOME,VARIAVEL,SIGLA,ABREVIACAO,DESCRICAO,CODUNIDADEMEDIDA,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@Nome,@Codigo,@Sigla,@Abreviacao,'',@unit,@codUsuario,@now) RETURNING CODVARIAVEL", new { item.Nome, item.Codigo, item.Sigla, item.Abreviacao, unit, codUsuario, now = DateTime.Now }, tx); foreach (var norm in req.Normalidades.Where(n => n.Variavel.Equals(item.Codigo, StringComparison.OrdinalIgnoreCase))) { var refId = await conn.QueryFirstOrDefaultAsync<int?>("SELECT CODREFERENCIA FROM REFERENCIA WHERE UPPER(TITULO)=UPPER(@Referencia) AND ANO='Unknown'", norm, tx); if (!refId.HasValue) refId = await conn.ExecuteScalarAsync<int>("INSERT INTO REFERENCIA (TITULO,ANO,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@Referencia,'Unknown',@codUsuario,@now) RETURNING CODREFERENCIA", new { norm.Referencia, codUsuario, now = DateTime.Now }, tx); await conn.ExecuteAsync("INSERT INTO NORMALIDADE (CODVARIAVEL,CODREFERENCIA,VALORMIN,VALORMAX,SEXO,IDADE_MIN,IDADE_MAX,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@varId,@refId,@ValorMin,@ValorMax,@Sexo,@IdadeMin,@IdadeMax,@codUsuario,@now)", new { varId, refId, norm.ValorMin, norm.ValorMax, norm.Sexo, norm.IdadeMin, norm.IdadeMax, codUsuario, now = DateTime.Now }, tx); } foreach (var formula in req.Formulas.Where(f => f.Variavel.Equals(item.Codigo, StringComparison.OrdinalIgnoreCase))) { var formId = await conn.ExecuteScalarAsync<int>("INSERT INTO FORMULAS (NOME,DESCRICAO,FORMULA,CASADECIMAIS,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@Sigla,@description,@Expressao,@CasasDecimais,@codUsuario,@now) RETURNING CODFORMULA", new { item.Sigla, description = $"Fórmula para {item.Sigla}", formula.Expressao, formula.CasasDecimais, codUsuario, now = DateTime.Now }, tx); await conn.ExecuteAsync("INSERT INTO FORMULA_VARIAVEL (CODFORMULA,CODVARIAVEL,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@formId,@varId,@codUsuario,@now)", new { formId, varId, codUsuario, now = DateTime.Now }, tx); } inserted++; } await tx.CommitAsync(ct); return ApiResponse.Ok(new { inseridas = inserted, ignoradas = ignored }); }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    private static string Required(string? value, string field) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidOperationException($"{field} é obrigatório.");

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
        public int? CodGrupo { get; init; }
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
