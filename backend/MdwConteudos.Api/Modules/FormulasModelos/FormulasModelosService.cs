using System.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.FormulasModelos;

public interface IFormulasModelosService
{
    Task<PagedResult<FormulaListItem>> ListFormulasAsync(int page, int pageSize, string? sigla, string? nome, string? formula, CancellationToken ct);
    Task<FormulaMeta> GetFormulaMetaAsync(CancellationToken ct);
    Task<FormulaDetalhe?> GetFormulaAsync(int id, CancellationToken ct);
    Task<int> CreateFormulaAsync(FormulaUpsertRequest request, int codUsuario, CancellationToken ct);
    Task UpdateFormulaAsync(int id, FormulaUpsertRequest request, int codUsuario, CancellationToken ct);
    Task DeleteFormulaAsync(int id, CancellationToken ct);

    Task<PagedResult<ModeloListItem>> ListModelosAsync(int codUsuario, int page, int pageSize, string? nome, CancellationToken ct);
    Task<ModeloDetalhe?> GetModeloAsync(int id, int codUsuario, CancellationToken ct);
    Task<IReadOnlyList<VariavelOpcao>> ListVariaveisAsync(CancellationToken ct);
    Task<int> CreateModeloAsync(ModeloUpsertRequest request, int codUsuario, CancellationToken ct);
    Task UpdateModeloAsync(int id, ModeloUpsertRequest request, int codUsuario, CancellationToken ct);
    Task DeleteModeloAsync(int id, int codUsuario, CancellationToken ct);
    Task<int> CreateSecaoAsync(int modeloId, SecaoUpsertRequest request, int codUsuario, CancellationToken ct);
    Task UpdateSecaoAsync(int modeloId, int secaoId, SecaoUpsertRequest request, int codUsuario, CancellationToken ct);
    Task DeleteSecaoAsync(int modeloId, int secaoId, int codUsuario, CancellationToken ct);
    Task OrderSecoesAsync(int modeloId, IReadOnlyList<int> secaoIds, int codUsuario, CancellationToken ct);
    Task OrderVariaveisAsync(int modeloId, int secaoId, IReadOnlyList<int> variavelIds, int codUsuario, CancellationToken ct);
    Task SaveLayoutAsync(int modeloId, IReadOnlyList<LayoutItemRequest> layout, int codUsuario, CancellationToken ct);
    Task<GeneratedModel> GenerateModeloAsync(int modeloId, int codUsuario, string formato, CancellationToken ct);
}

public sealed class FormulasModelosService : IFormulasModelosService
{
    private readonly IFirebirdConnectionFactory _connections;

    public FormulasModelosService(IFirebirdConnectionFactory connections) => _connections = connections;

    public async Task<PagedResult<FormulaListItem>> ListFormulasAsync(
        int page,
        int pageSize,
        string? sigla,
        string? nome,
        string? formula,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;
        var where = new List<string> { "1=1" };
        var parameters = new DynamicParameters();

        AddLike(where, parameters, "UPPER(V.SIGLA) LIKE UPPER(@sigla)", "sigla", sigla);
        AddLike(where, parameters, "UPPER(F.NOME) LIKE UPPER(@nome)", "nome", nome);
        AddLike(where, parameters, "UPPER(F.FORMULA) LIKE UPPER(@formula)", "formula", formula);

        var whereSql = string.Join(" AND ", where);
        await using var connection = await _connections.OpenConnectionAsync(ct);
        var total = await connection.ExecuteScalarAsync<int>(Cmd($@"
            SELECT COUNT(DISTINCT F.CODFORMULA)
            FROM FORMULAS F
            JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
            JOIN VARIAVEIS V ON V.CODVARIAVEL = FV.CODVARIAVEL
            WHERE {whereSql}", parameters, ct: ct));

        parameters.Add("first", pageSize);
        parameters.Add("skip", skip);
        var rows = await connection.QueryAsync<FormulaListItem>(Cmd($@"
            SELECT FIRST @first SKIP @skip
                F.CODFORMULA AS CodFormula,
                COALESCE(F.NOME, '') AS Nome,
                COALESCE(F.FORMULA, '') AS Formula,
                F.DESCRICAO AS Descricao,
                COALESCE(F.CASADECIMAIS, 2) AS CasasDecimais,
                LIST(V.NOME, ', ') AS Variaveis,
                LIST(V.SIGLA, ', ') AS Siglas
            FROM FORMULAS F
            JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
            JOIN VARIAVEIS V ON V.CODVARIAVEL = FV.CODVARIAVEL
            WHERE {whereSql}
            GROUP BY F.CODFORMULA, F.NOME, F.FORMULA, F.DESCRICAO, F.CASADECIMAIS
            ORDER BY F.NOME, F.CODFORMULA", parameters, ct: ct));

        return Page(rows.AsList(), page, pageSize, total);
    }

    public async Task<FormulaMeta> GetFormulaMetaAsync(CancellationToken ct)
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        var variaveis = (await connection.QueryAsync<VariavelOpcao>(Cmd(@"
            SELECT
                V.CODVARIAVEL AS CodVariavel,
                COALESCE(V.NOME, '') AS Nome,
                COALESCE(V.SIGLA, '') AS Sigla,
                U.DESCRICAO AS Unidade,
                V.ABREVIACAO AS Abreviacao,
                (SELECT FIRST 1 F.FORMULA
                   FROM FORMULA_VARIAVEL FV
                   JOIN FORMULAS F ON F.CODFORMULA = FV.CODFORMULA
                  WHERE FV.CODVARIAVEL = V.CODVARIAVEL) AS Formula,
                (SELECT LIST(COALESCE(N.SEXO, '-') || ': ' || COALESCE(CAST(N.VALORMIN AS VARCHAR(30)), '-') || ' a ' || COALESCE(CAST(N.VALORMAX AS VARCHAR(30)), '-'), '; ')
                   FROM NORMALIDADE N
                  WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS Normalidade
            FROM VARIAVEIS V
            LEFT JOIN UNIDADEMEDIDA U ON U.CODUNIDADEMEDIDA = V.CODUNIDADEMEDIDA
            ORDER BY V.NOME, V.SIGLA", ct: ct))).AsList();

        var linguagens = (await connection.QueryAsync<LinguagemOpcao>(Cmd(@"
            SELECT CODLINGUAGEM AS CodLinguagem, COALESCE(NOME, '') AS Nome
            FROM TIPOLINGUAGEM
            ORDER BY NOME", ct: ct))).AsList();

        var referencias = (await connection.QueryAsync<ReferenciaOpcao>(Cmd(@"
            SELECT R.CODREFERENCIA AS CodReferencia,
                   COALESCE(R.TITULO, '') AS Titulo,
                   R.ANO AS Ano,
                   LIST(A.NOME, ', ') AS Autores
            FROM REFERENCIA R
            LEFT JOIN REFERENCIA_AUTORES RA ON RA.CODREFERENCIA = R.CODREFERENCIA
            LEFT JOIN AUTORES A ON A.CODAUTOR = RA.CODAUTOR
            GROUP BY R.CODREFERENCIA, R.TITULO, R.ANO
            ORDER BY R.ANO DESC NULLS LAST, R.TITULO", ct: ct))).AsList();

        return new FormulaMeta(variaveis, linguagens, referencias);
    }

    public async Task<FormulaDetalhe?> GetFormulaAsync(int id, CancellationToken ct)
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        var formula = await connection.QueryFirstOrDefaultAsync<FormulaRow>(Cmd(@"
            SELECT CODFORMULA AS CodFormula,
                   COALESCE(NOME, '') AS Nome,
                   COALESCE(FORMULA, '') AS Formula,
                   DESCRICAO AS Descricao,
                   COALESCE(CASADECIMAIS, 2) AS CasasDecimais
            FROM FORMULAS
            WHERE CODFORMULA = @id", new { id }, ct: ct));
        if (formula is null) return null;

        var variavelIds = (await connection.QueryAsync<int>(Cmd(@"
            SELECT CODVARIAVEL
            FROM FORMULA_VARIAVEL
            WHERE CODFORMULA = @id
            ORDER BY CODVARIAVEL", new { id }, ct: ct))).AsList();

        var equacoes = (await connection.QueryAsync<EquacaoDto>(Cmd(@"
            SELECT EL.CODLINGUAGEM AS CodLinguagem,
                   COALESCE(L.NOME, '') AS Linguagem,
                   EL.CODREFERENCIA AS CodReferencia,
                   CASE WHEN R.CODREFERENCIA IS NULL THEN NULL
                        ELSE R.TITULO || COALESCE(' (' || CAST(R.ANO AS VARCHAR(10)) || ')', '')
                   END AS Referencia,
                   COALESCE(EL.EQUACAO, '') AS Equacao,
                   EL.NOME_FUNCAO AS NomeFuncao
            FROM EQUACOES_LINGUAGEM EL
            JOIN TIPOLINGUAGEM L ON L.CODLINGUAGEM = EL.CODLINGUAGEM
            LEFT JOIN REFERENCIA R ON R.CODREFERENCIA = EL.CODREFERENCIA
            WHERE EL.CODFORMULA = @id
            ORDER BY L.NOME, EL.CODEQUACAO", new { id }, ct: ct))).AsList();

        return new FormulaDetalhe(
            formula.CodFormula,
            formula.Nome,
            formula.Formula,
            formula.Descricao,
            formula.CasasDecimais,
            variavelIds,
            equacoes);
    }

    public async Task<int> CreateFormulaAsync(FormulaUpsertRequest request, int codUsuario, CancellationToken ct)
    {
        ValidateFormula(request);
        var variableIds = DistinctPositive(request.VariavelIds);
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            var variables = await LoadAndValidateVariables(connection, transaction, variableIds, null, ct);
            var name = string.IsNullOrWhiteSpace(request.Nome) ? variables[0].Sigla : request.Nome.Trim();
            var description = string.IsNullOrWhiteSpace(request.Descricao)
                ? $"Fórmula para {variables[0].Nome}"
                : request.Descricao.Trim();

            var id = await connection.ExecuteScalarAsync<int>(Cmd(@"
                INSERT INTO FORMULAS (NOME, DESCRICAO, FORMULA, CASADECIMAIS, DTHRULTMODIFICACAO, CODUSUARIO)
                VALUES (@name, @description, @formula, @decimalPlaces, @now, @codUsuario)
                RETURNING CODFORMULA", new
            {
                name,
                description,
                formula = request.Formula.Trim(),
                decimalPlaces = request.CasasDecimais,
                now = DateTime.Now,
                codUsuario
            }, transaction, ct));

            await ReplaceFormulaLinks(connection, transaction, id, variableIds, request.Equacoes ?? [], codUsuario, ct);
            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateFormulaAsync(int id, FormulaUpsertRequest request, int codUsuario, CancellationToken ct)
    {
        ValidateFormula(request);
        var variableIds = DistinctPositive(request.VariavelIds);
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            if (await connection.ExecuteScalarAsync<int>(Cmd(
                    "SELECT COUNT(*) FROM FORMULAS WHERE CODFORMULA = @id",
                    new { id }, transaction, ct)) == 0)
                throw new InvalidOperationException("Fórmula não encontrada.");

            var variables = await LoadAndValidateVariables(connection, transaction, variableIds, id, ct);
            var name = string.IsNullOrWhiteSpace(request.Nome) ? variables[0].Sigla : request.Nome.Trim();
            await connection.ExecuteAsync(Cmd(@"
                UPDATE FORMULAS
                   SET NOME = @name,
                       DESCRICAO = @description,
                       FORMULA = @formula,
                       CASADECIMAIS = @decimalPlaces,
                       DTHRULTMODIFICACAO = @now,
                       CODUSUARIO = @codUsuario
                 WHERE CODFORMULA = @id", new
            {
                id,
                name,
                description = NullIfWhiteSpace(request.Descricao),
                formula = request.Formula.Trim(),
                decimalPlaces = request.CasasDecimais,
                now = DateTime.Now,
                codUsuario
            }, transaction, ct));

            await ReplaceFormulaLinks(connection, transaction, id, variableIds, request.Equacoes ?? [], codUsuario, ct);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteFormulaAsync(int id, CancellationToken ct)
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            if (await connection.ExecuteScalarAsync<int>(Cmd(
                    "SELECT COUNT(*) FROM FORMULAS WHERE CODFORMULA = @id",
                    new { id }, transaction, ct)) == 0)
                throw new InvalidOperationException("Fórmula não encontrada.");

            await connection.ExecuteAsync(Cmd("DELETE FROM EQUACOES_LINGUAGEM WHERE CODFORMULA = @id", new { id }, transaction, ct));
            await connection.ExecuteAsync(Cmd("DELETE FROM FORMULA_VARIAVEL WHERE CODFORMULA = @id", new { id }, transaction, ct));
            await connection.ExecuteAsync(Cmd("DELETE FROM FORMULAS WHERE CODFORMULA = @id", new { id }, transaction, ct));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<PagedResult<ModeloListItem>> ListModelosAsync(
        int codUsuario,
        int page,
        int pageSize,
        string? nome,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var parameters = new DynamicParameters(new { codUsuario });
        var filter = string.Empty;
        if (!string.IsNullOrWhiteSpace(nome))
        {
            filter = " AND UPPER(M.NOME) LIKE UPPER(@nome)";
            parameters.Add("nome", $"%{nome.Trim()}%");
        }

        await using var connection = await _connections.OpenConnectionAsync(ct);
        var total = await connection.ExecuteScalarAsync<int>(Cmd($@"
            SELECT COUNT(*) FROM MODELO_MODO_TEXTO M
            WHERE M.CODUSUARIO = @codUsuario {filter}", parameters, ct: ct));

        parameters.Add("first", pageSize);
        parameters.Add("skip", (page - 1) * pageSize);
        var items = (await connection.QueryAsync<ModeloListItem>(Cmd($@"
            SELECT FIRST @first SKIP @skip
                   M.CODMODELO AS CodModelo,
                   COALESCE(M.NOME, '') AS Nome,
                   (SELECT COUNT(*) FROM SECAO_MODO_TEXTO S WHERE S.CODMODELO = M.CODMODELO) AS TotalSecoes
            FROM MODELO_MODO_TEXTO M
            WHERE M.CODUSUARIO = @codUsuario {filter}
            ORDER BY M.NOME, M.CODMODELO", parameters, ct: ct))).AsList();

        return Page(items, page, pageSize, total);
    }

    public async Task<ModeloDetalhe?> GetModeloAsync(int id, int codUsuario, CancellationToken ct)
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        var model = await connection.QueryFirstOrDefaultAsync<ModeloRow>(Cmd(@"
            SELECT CODMODELO AS CodModelo, COALESCE(NOME, '') AS Nome
            FROM MODELO_MODO_TEXTO
            WHERE CODMODELO = @id AND CODUSUARIO = @codUsuario",
            new { id, codUsuario }, ct: ct));
        if (model is null) return null;

        var sections = (await connection.QueryAsync<SectionRow>(Cmd(@"
            SELECT CODSECAO AS CodSecao,
                   COALESCE(NOME, '') AS Nome,
                   COALESCE(ORDEM, 0) AS Ordem,
                   COALESCE(POSICAO_X, 0) AS X,
                   COALESCE(POSICAO_Y, 0) AS Y,
                   COALESCE(LARGURA, 6) AS Largura,
                   COALESCE(ALTURA, 4) AS Altura
            FROM SECAO_MODO_TEXTO
            WHERE CODMODELO = @id
            ORDER BY ORDEM, POSICAO_Y, POSICAO_X, NOME", new { id }, ct: ct))).AsList();

        var result = new List<SecaoDto>(sections.Count);
        foreach (var section in sections)
        {
            var variables = (await connection.QueryAsync<SecaoVariavelDto>(Cmd(@"
                SELECT V.CODVARIAVEL AS CodVariavel,
                       COALESCE(V.NOME, '') AS Nome,
                       COALESCE(V.SIGLA, '') AS Sigla,
                       COALESCE(SV.EXIBIR_GRAFICO, 0) AS ExibirGrafico,
                       COALESCE(SV.ORDEM, 0) AS Ordem,
                       U.DESCRICAO AS Unidade,
                       (SELECT LIST(COALESCE(N.SEXO, '-') || ': ' || COALESCE(CAST(N.VALORMIN AS VARCHAR(30)), '') || ' a ' || COALESCE(CAST(N.VALORMAX AS VARCHAR(30)), ''), '; ')
                          FROM NORMALIDADE N
                         WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS Normalidade
                FROM SECAO_VARIAVEL SV
                JOIN VARIAVEIS V ON V.CODVARIAVEL = SV.CODVARIAVEL
                LEFT JOIN UNIDADEMEDIDA U ON U.CODUNIDADEMEDIDA = V.CODUNIDADEMEDIDA
                WHERE SV.CODSECAO = @sectionId
                ORDER BY SV.ORDEM, V.SIGLA", new { sectionId = section.CodSecao }, ct: ct))).AsList();

            result.Add(new SecaoDto(
                section.CodSecao,
                section.Nome,
                section.Ordem,
                section.X,
                section.Y,
                section.Largura,
                section.Altura,
                variables));
        }

        return new ModeloDetalhe(model.CodModelo, model.Nome, result);
    }

    public async Task<IReadOnlyList<VariavelOpcao>> ListVariaveisAsync(CancellationToken ct)
        => (await GetFormulaMetaAsync(ct)).Variaveis;

    public async Task<int> CreateModeloAsync(ModeloUpsertRequest request, int codUsuario, CancellationToken ct)
    {
        var name = RequiredName(request.Nome, "O nome do modelo é obrigatório.");
        await using var connection = await _connections.OpenConnectionAsync(ct);
        if (await connection.ExecuteScalarAsync<int>(Cmd(@"
                SELECT COUNT(*) FROM MODELO_MODO_TEXTO
                WHERE UPPER(NOME) = UPPER(@name) AND CODUSUARIO = @codUsuario",
                new { name, codUsuario }, ct: ct)) > 0)
            throw new InvalidOperationException("Já existe um modelo com esse nome.");

        return await connection.ExecuteScalarAsync<int>(Cmd(@"
            INSERT INTO MODELO_MODO_TEXTO (NOME, CODUSUARIO)
            VALUES (@name, @codUsuario)
            RETURNING CODMODELO", new { name, codUsuario }, ct: ct));
    }

    public async Task UpdateModeloAsync(int id, ModeloUpsertRequest request, int codUsuario, CancellationToken ct)
    {
        var name = RequiredName(request.Nome, "O nome do modelo é obrigatório.");
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureModelOwnership(connection, transaction, id, codUsuario, ct);
            if (await connection.ExecuteScalarAsync<int>(Cmd(@"
                    SELECT COUNT(*) FROM MODELO_MODO_TEXTO
                    WHERE UPPER(NOME) = UPPER(@name) AND CODUSUARIO = @codUsuario AND CODMODELO <> @id",
                    new { name, codUsuario, id }, transaction, ct)) > 0)
                throw new InvalidOperationException("Já existe um modelo com esse nome.");

            await connection.ExecuteAsync(Cmd(@"
                UPDATE MODELO_MODO_TEXTO
                SET NOME = @name, DTHRULTMODIFICACAO = @now
                WHERE CODMODELO = @id AND CODUSUARIO = @codUsuario",
                new { name, now = DateTime.Now, id, codUsuario }, transaction, ct));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteModeloAsync(int id, int codUsuario, CancellationToken ct)
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureModelOwnership(connection, transaction, id, codUsuario, ct);
            await connection.ExecuteAsync(Cmd(@"
                DELETE FROM SECAO_VARIAVEL
                WHERE CODSECAO IN (SELECT CODSECAO FROM SECAO_MODO_TEXTO WHERE CODMODELO = @id)",
                new { id }, transaction, ct));
            await connection.ExecuteAsync(Cmd("DELETE FROM SECAO_MODO_TEXTO WHERE CODMODELO = @id", new { id }, transaction, ct));
            await connection.ExecuteAsync(Cmd(
                "DELETE FROM MODELO_MODO_TEXTO WHERE CODMODELO = @id AND CODUSUARIO = @codUsuario",
                new { id, codUsuario }, transaction, ct));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> CreateSecaoAsync(int modeloId, SecaoUpsertRequest request, int codUsuario, CancellationToken ct)
    {
        var name = RequiredName(request.Nome, "O nome da seção é obrigatório.");
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureModelOwnership(connection, transaction, modeloId, codUsuario, ct);
            await EnsureSectionNameAvailable(connection, transaction, modeloId, null, name, ct);
            var variables = NormalizeSectionVariables(request.Variaveis);
            await ValidateVariableIds(connection, transaction, variables.Select(x => x.CodVariavel).ToArray(), ct);
            var order = await connection.ExecuteScalarAsync<int>(Cmd(@"
                SELECT COALESCE(MAX(ORDEM), 0) + 1 FROM SECAO_MODO_TEXTO WHERE CODMODELO = @modeloId",
                new { modeloId }, transaction, ct));
            var sectionId = await connection.ExecuteScalarAsync<int>(Cmd(@"
                INSERT INTO SECAO_MODO_TEXTO (NOME, CODMODELO, ORDEM)
                VALUES (@name, @modeloId, @order)
                RETURNING CODSECAO", new { name, modeloId, order }, transaction, ct));
            await InsertSectionVariables(connection, transaction, sectionId, variables, ct);
            transaction.Commit();
            return sectionId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateSecaoAsync(int modeloId, int secaoId, SecaoUpsertRequest request, int codUsuario, CancellationToken ct)
    {
        var name = RequiredName(request.Nome, "O nome da seção é obrigatório.");
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureSectionOwnership(connection, transaction, modeloId, secaoId, codUsuario, ct);
            await EnsureSectionNameAvailable(connection, transaction, modeloId, secaoId, name, ct);
            var variables = NormalizeSectionVariables(request.Variaveis);
            await ValidateVariableIds(connection, transaction, variables.Select(x => x.CodVariavel).ToArray(), ct);
            await connection.ExecuteAsync(Cmd(@"
                UPDATE SECAO_MODO_TEXTO SET NOME = @name
                WHERE CODSECAO = @secaoId AND CODMODELO = @modeloId",
                new { name, secaoId, modeloId }, transaction, ct));
            await connection.ExecuteAsync(Cmd("DELETE FROM SECAO_VARIAVEL WHERE CODSECAO = @secaoId", new { secaoId }, transaction, ct));
            await InsertSectionVariables(connection, transaction, secaoId, variables, ct);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteSecaoAsync(int modeloId, int secaoId, int codUsuario, CancellationToken ct)
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureSectionOwnership(connection, transaction, modeloId, secaoId, codUsuario, ct);
            await connection.ExecuteAsync(Cmd("DELETE FROM SECAO_VARIAVEL WHERE CODSECAO = @secaoId", new { secaoId }, transaction, ct));
            await connection.ExecuteAsync(Cmd(@"
                DELETE FROM SECAO_MODO_TEXTO WHERE CODSECAO = @secaoId AND CODMODELO = @modeloId",
                new { secaoId, modeloId }, transaction, ct));
            await ReindexSections(connection, transaction, modeloId, ct);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task OrderSecoesAsync(int modeloId, IReadOnlyList<int> secaoIds, int codUsuario, CancellationToken ct)
    {
        var ids = DistinctPositive(secaoIds);
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureModelOwnership(connection, transaction, modeloId, codUsuario, ct);
            var existing = (await connection.QueryAsync<int>(Cmd(
                "SELECT CODSECAO FROM SECAO_MODO_TEXTO WHERE CODMODELO = @modeloId",
                new { modeloId }, transaction, ct))).OrderBy(x => x).ToArray();
            if (!existing.SequenceEqual(ids.OrderBy(x => x)))
                throw new InvalidOperationException("A ordenação deve conter todas as seções do modelo, sem repetições.");

            for (var i = 0; i < ids.Count; i++)
                await connection.ExecuteAsync(Cmd(@"
                    UPDATE SECAO_MODO_TEXTO SET ORDEM = @order
                    WHERE CODSECAO = @sectionId AND CODMODELO = @modeloId",
                    new { order = i + 1, sectionId = ids[i], modeloId }, transaction, ct));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task OrderVariaveisAsync(int modeloId, int secaoId, IReadOnlyList<int> variavelIds, int codUsuario, CancellationToken ct)
    {
        var ids = DistinctPositive(variavelIds);
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureSectionOwnership(connection, transaction, modeloId, secaoId, codUsuario, ct);
            var existing = (await connection.QueryAsync<int>(Cmd(
                "SELECT CODVARIAVEL FROM SECAO_VARIAVEL WHERE CODSECAO = @secaoId",
                new { secaoId }, transaction, ct))).OrderBy(x => x).ToArray();
            if (!existing.SequenceEqual(ids.OrderBy(x => x)))
                throw new InvalidOperationException("A ordenação deve conter todas as variáveis da seção, sem repetições.");

            for (var i = 0; i < ids.Count; i++)
                await connection.ExecuteAsync(Cmd(@"
                    UPDATE SECAO_VARIAVEL SET ORDEM = @order
                    WHERE CODSECAO = @secaoId AND CODVARIAVEL = @variableId",
                    new { order = i + 1, secaoId, variableId = ids[i] }, transaction, ct));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task SaveLayoutAsync(int modeloId, IReadOnlyList<LayoutItemRequest> layout, int codUsuario, CancellationToken ct)
    {
        var duplicate = layout.GroupBy(x => x.CodSecao).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException("O layout contém seções repetidas.");
        if (layout.Any(x => x.CodSecao <= 0 || x.X < 0 || x.Y < 0 || x.Largura is < 1 or > 12 || x.Altura is < 1 or > 100 || x.X + x.Largura > 12))
            throw new InvalidOperationException("Coordenadas ou dimensões inválidas no layout.");

        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureModelOwnership(connection, transaction, modeloId, codUsuario, ct);
            var existing = (await connection.QueryAsync<int>(Cmd(
                "SELECT CODSECAO FROM SECAO_MODO_TEXTO WHERE CODMODELO = @modeloId",
                new { modeloId }, transaction, ct))).OrderBy(x => x).ToArray();
            if (!existing.SequenceEqual(layout.Select(x => x.CodSecao).OrderBy(x => x)))
                throw new InvalidOperationException("O layout deve conter todas as seções do modelo.");

            foreach (var item in layout)
                await connection.ExecuteAsync(Cmd(@"
                    UPDATE SECAO_MODO_TEXTO
                    SET POSICAO_X = @X, POSICAO_Y = @Y, LARGURA = @Largura, ALTURA = @Altura
                    WHERE CODSECAO = @CodSecao AND CODMODELO = @modeloId",
                    new { item.X, item.Y, item.Largura, item.Altura, item.CodSecao, modeloId }, transaction, ct));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<GeneratedModel> GenerateModeloAsync(int modeloId, int codUsuario, string formato, CancellationToken ct)
    {
        var model = await GetModeloAsync(modeloId, codUsuario, ct)
            ?? throw new InvalidOperationException("Modelo não encontrado ou sem permissão de acesso.");
        var safeName = Regex.Replace(model.Nome, @"[^\p{L}\p{N}_-]+", "_").Trim('_');
        if (string.Equals(formato, "texto", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(formato, "txt", StringComparison.OrdinalIgnoreCase))
        {
            return new GeneratedModel(
                $"modelo_{modeloId}_{safeName}.txt",
                GenerateText(model),
                "text/plain");
        }

        if (!string.Equals(formato, "html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Formato inválido. Use 'html' ou 'texto'.");

        return new GeneratedModel(
            $"modelo_{modeloId}_{safeName}.html",
            GenerateHtml(model),
            "text/html");
    }

    private static async Task<IReadOnlyList<VariableRow>> LoadAndValidateVariables(
        FbConnection connection,
        IDbTransaction transaction,
        IReadOnlyList<int> ids,
        int? currentFormulaId,
        CancellationToken ct)
    {
        if (ids.Count == 0) throw new InvalidOperationException("Selecione ao menos uma variável.");
        var variables = (await connection.QueryAsync<VariableRow>(Cmd(@"
            SELECT CODVARIAVEL AS CodVariavel, COALESCE(NOME, '') AS Nome, COALESCE(SIGLA, '') AS Sigla
            FROM VARIAVEIS WHERE CODVARIAVEL IN @ids ORDER BY NOME", new { ids }, transaction, ct))).AsList();
        if (variables.Count != ids.Count) throw new InvalidOperationException("Uma ou mais variáveis não existem.");

        var conflictSql = currentFormulaId.HasValue
            ? "SELECT COUNT(*) FROM FORMULA_VARIAVEL WHERE CODVARIAVEL IN @ids AND CODFORMULA <> @currentFormulaId"
            : "SELECT COUNT(*) FROM FORMULA_VARIAVEL WHERE CODVARIAVEL IN @ids";
        var conflicts = await connection.ExecuteScalarAsync<int>(Cmd(
            conflictSql,
            new { ids, currentFormulaId },
            transaction,
            ct));
        if (conflicts > 0) throw new InvalidOperationException("Uma ou mais variáveis já estão vinculadas a outra fórmula.");
        return variables;
    }

    private static async Task ReplaceFormulaLinks(
        FbConnection connection,
        IDbTransaction transaction,
        int formulaId,
        IReadOnlyList<int> variableIds,
        IReadOnlyList<EquacaoRequest> equations,
        int codUsuario,
        CancellationToken ct)
    {
        if (equations.Any(x => x.CodLinguagem <= 0 || string.IsNullOrWhiteSpace(x.Equacao)))
            throw new InvalidOperationException("Toda equação deve ter linguagem e conteúdo.");

        await connection.ExecuteAsync(Cmd("DELETE FROM EQUACOES_LINGUAGEM WHERE CODFORMULA = @formulaId", new { formulaId }, transaction, ct));
        await connection.ExecuteAsync(Cmd("DELETE FROM FORMULA_VARIAVEL WHERE CODFORMULA = @formulaId", new { formulaId }, transaction, ct));
        foreach (var variableId in variableIds)
            await connection.ExecuteAsync(Cmd(@"
                INSERT INTO FORMULA_VARIAVEL (CODFORMULA, CODVARIAVEL, CODUSUARIO, DTHRULTMODIFICACAO)
                VALUES (@formulaId, @variableId, @codUsuario, @now)",
                new { formulaId, variableId, codUsuario, now = DateTime.Now }, transaction, ct));

        foreach (var equation in equations)
            await connection.ExecuteAsync(Cmd(@"
                INSERT INTO EQUACOES_LINGUAGEM (CODFORMULA, CODLINGUAGEM, CODREFERENCIA, EQUACAO, NOME_FUNCAO)
                VALUES (@formulaId, @languageId, @referenceId, @equation, @functionName)", new
            {
                formulaId,
                languageId = equation.CodLinguagem,
                referenceId = equation.CodReferencia,
                equation = equation.Equacao.Trim(),
                functionName = NullIfWhiteSpace(equation.NomeFuncao)
            }, transaction, ct));
    }

    private static async Task EnsureModelOwnership(FbConnection connection, IDbTransaction transaction, int modelId, int userId, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(Cmd(@"
                SELECT COUNT(*) FROM MODELO_MODO_TEXTO
                WHERE CODMODELO = @modelId AND CODUSUARIO = @userId",
                new { modelId, userId }, transaction, ct)) == 0)
            throw new InvalidOperationException("Modelo não encontrado ou sem permissão de acesso.");
    }

    private static async Task EnsureSectionOwnership(FbConnection connection, IDbTransaction transaction, int modelId, int sectionId, int userId, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(Cmd(@"
                SELECT COUNT(*)
                FROM SECAO_MODO_TEXTO S
                JOIN MODELO_MODO_TEXTO M ON M.CODMODELO = S.CODMODELO
                WHERE S.CODSECAO = @sectionId AND S.CODMODELO = @modelId AND M.CODUSUARIO = @userId",
                new { sectionId, modelId, userId }, transaction, ct)) == 0)
            throw new InvalidOperationException("Seção não encontrada ou sem permissão de acesso.");
    }

    private static async Task EnsureSectionNameAvailable(FbConnection connection, IDbTransaction transaction, int modelId, int? sectionId, string name, CancellationToken ct)
    {
        var sql = sectionId.HasValue
            ? @"SELECT COUNT(*) FROM SECAO_MODO_TEXTO
                WHERE CODMODELO = @modelId AND UPPER(NOME) = UPPER(@name) AND CODSECAO <> @sectionId"
            : @"SELECT COUNT(*) FROM SECAO_MODO_TEXTO
                WHERE CODMODELO = @modelId AND UPPER(NOME) = UPPER(@name)";
        if (await connection.ExecuteScalarAsync<int>(Cmd(
                sql,
                new { modelId, sectionId, name },
                transaction,
                ct)) > 0)
            throw new InvalidOperationException("Já existe uma seção com esse nome neste modelo.");
    }

    private static async Task ValidateVariableIds(FbConnection connection, IDbTransaction transaction, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var count = await connection.ExecuteScalarAsync<int>(Cmd(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE CODVARIAVEL IN @ids",
            new { ids }, transaction, ct));
        if (count != ids.Count) throw new InvalidOperationException("Uma ou mais variáveis não existem.");
    }

    private static async Task InsertSectionVariables(FbConnection connection, IDbTransaction transaction, int sectionId, IReadOnlyList<SecaoVariavelRequest> variables, CancellationToken ct)
    {
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            await connection.ExecuteAsync(Cmd(@"
                INSERT INTO SECAO_VARIAVEL (CODSECAO, CODVARIAVEL, EXIBIR_GRAFICO, ORDEM)
                VALUES (@sectionId, @variableId, @showChart, @order)", new
            {
                sectionId,
                variableId = variable.CodVariavel,
                showChart = variable.ExibirGrafico ? 1 : 0,
                order = i + 1
            }, transaction, ct));
        }
    }

    private static async Task ReindexSections(FbConnection connection, IDbTransaction transaction, int modelId, CancellationToken ct)
    {
        var ids = (await connection.QueryAsync<int>(Cmd(@"
            SELECT CODSECAO FROM SECAO_MODO_TEXTO
            WHERE CODMODELO = @modelId ORDER BY ORDEM, NOME",
            new { modelId }, transaction, ct))).AsList();
        for (var i = 0; i < ids.Count; i++)
            await connection.ExecuteAsync(Cmd(
                "UPDATE SECAO_MODO_TEXTO SET ORDEM = @order WHERE CODSECAO = @id",
                new { order = i + 1, id = ids[i] }, transaction, ct));
    }

    private static IReadOnlyList<SecaoVariavelRequest> NormalizeSectionVariables(IReadOnlyList<SecaoVariavelRequest>? variables)
    {
        var items = (variables ?? [])
            .Where(x => x.CodVariavel > 0)
            .OrderBy(x => x.Ordem ?? int.MaxValue)
            .ToList();
        if (items.Select(x => x.CodVariavel).Distinct().Count() != items.Count)
            throw new InvalidOperationException("A seção contém variáveis repetidas.");
        return items;
    }

    private static string GenerateText(ModeloDetalhe model)
    {
        var builder = new StringBuilder();
        foreach (var section in model.Secoes.OrderBy(x => x.Ordem))
        {
            builder.AppendLine($"[{section.Nome.ToUpperInvariant()}]");
            foreach (var variable in section.Variaveis.OrderBy(x => x.Ordem))
            {
                var normality = string.IsNullOrWhiteSpace(variable.Normalidade)
                    ? ""
                    : " " + string.Join(" ", variable.Normalidade.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => $"({x})"));
                builder.AppendLine($"{variable.Nome} ({NormalizeVariableToken(variable.Sigla)}): 0.0  {variable.Unidade ?? "sem unidade"}{normality}");
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string NormalizeVariableToken(string value)
    {
        var token = Regex.Replace(value ?? "", @"^VR_", "", RegexOptions.IgnoreCase);
        token = Regex.Replace(token, @"[^A-Za-z0-9_]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(token) ? "CAMPO" : token;
    }

    private static string GenerateHtml(ModeloDetalhe model)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        static string Id(string value) => Regex.Replace(value, @"[^A-Za-z0-9_-]", string.Empty);
        var buttons = new StringBuilder();
        var sections = new StringBuilder();
        foreach (var section in model.Secoes.OrderBy(x => x.Ordem).ThenBy(x => x.Y).ThenBy(x => x.X))
        {
            var sectionId = $"Collapse{section.CodSecao}_{Id(section.Nome)}";
            buttons.Append($"<label><input id=\"VR_EXP{section.CodSecao}\" type=\"checkbox\" checked data-target=\"{sectionId}\" onchange=\"ValidaCollapse()\"><span>{E(section.Nome)}</span></label>");
            sections.Append($"<section name=\"SectionContainer\" data-x=\"{section.X}\" data-y=\"{section.Y}\" data-w=\"{section.Largura}\" data-h=\"{section.Altura}\"><div id=\"{sectionId}\" class=\"bgMedidas\"><div class=\"tituloSessao\">{E(section.Nome.ToUpperInvariant())}</div>");
            foreach (var variable in section.Variaveis.OrderBy(x => x.Ordem))
            {
                var inputId = $"VR_{Id(variable.Sigla)}";
                sections.Append($"<div class=\"input-group\"><label for=\"{inputId}\">{E(variable.Nome)}</label><input class=\"campoMedida\" type=\"number\" step=\"any\" id=\"{inputId}\" autocomplete=\"off\"></div>");
                if (variable.ExibirGrafico)
                    sections.Append($"<div class=\"bar-container\" id=\"{inputId}_bar\"><i class=\"low\"></i><i class=\"moderate\"></i><i class=\"elevated\"></i><i class=\"high\"></i><b class=\"pointer\" id=\"{inputId}_pointer\"></b></div>");
            }
            sections.Append("</div></section>");
        }

        return $$"""
            <!doctype html>
            <html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{{E(model.Nome)}}</title>
            <style>
            body{font-family:Arial,sans-serif;color:#242424}.Script_menu{display:flex;flex-wrap:wrap;gap:8px;margin:10px 0}.Script_menu input{display:none}.Script_menu span{display:inline-block;background:#e6e6e6;padding:5px 10px;border-radius:7px}.Script_menu input:checked+span{color:#fff;background:#343a40}.tituloSessao{font-size:18px;background:#e6e6e6;border-radius:5px;padding:5px;margin:10px 0;font-weight:bold}.bgMedidas{background:#f8f9fa;border-radius:8px;padding:15px;margin-bottom:15px}.input-group{display:flex;align-items:center;gap:10px;margin-bottom:10px}.input-group label{width:220px;font-size:13px}.campoMedida{border:1px solid #e3e3e3;border-radius:5px;width:70px;height:30px;text-align:right}.bar-container{position:relative;width:180px;height:10px;display:flex;border-radius:50px;margin:0 0 12px 230px}.bar-container i{display:block;height:100%}.low{background:#2ecc71;width:70px}.moderate{background:#f1c40f;width:40px}.elevated{background:#e67e22;width:35px}.high{background:#e74c3c;width:35px}.pointer{position:absolute;top:-3px;left:0;width:12px;height:12px;background:#2c3e50;border:2px solid #fff;border-radius:50%;transform:translateX(-50%)}
            </style></head><body>
            <h1>{{E(model.Nome)}}</h1><div id="Script_menu" class="Script_menu">{{buttons}}</div>{{sections}}
            <script>
            function ValidaCollapse(){document.querySelectorAll('.Script_menu input').forEach(c=>{const t=document.getElementById(c.dataset.target);if(t)t.style.display=c.checked?'block':'none'})}
            document.querySelectorAll('input[id^="VR_"]:not([type="checkbox"])').forEach(c=>c.addEventListener('input',()=>{const p=document.getElementById(c.id+'_pointer');if(p)p.style.left=Math.min((parseFloat(c.value)||0)/100*180,180)+'px'}));ValidaCollapse();
            </script></body></html>
            """;
    }

    private static void ValidateFormula(FormulaUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Formula))
            throw new InvalidOperationException("A fórmula é obrigatória.");
        if (request.CasasDecimais < 0 || request.CasasDecimais > 15)
            throw new InvalidOperationException("Casas decimais deve estar entre 0 e 15.");
    }

    private static string RequiredName(string? value, string message)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(message) : value.Trim();

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<int> DistinctPositive(IEnumerable<int>? values)
        => (values ?? []).Where(x => x > 0).Distinct().ToList();

    private static void AddLike(List<string> where, DynamicParameters parameters, string clause, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        where.Add(clause);
        parameters.Add(name, $"%{value.Trim()}%");
    }

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize, int total)
        => new(items, page, pageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));

    private static CommandDefinition Cmd(string sql, object? parameters = null, IDbTransaction? transaction = null, CancellationToken ct = default)
        => new(sql, parameters, transaction, cancellationToken: ct);

    private sealed record FormulaRow(int CodFormula, string Nome, string Formula, string? Descricao, int CasasDecimais);
    private sealed record VariableRow(int CodVariavel, string Nome, string Sigla);
    private sealed record ModeloRow(int CodModelo, string Nome);
    private sealed record SectionRow(int CodSecao, string Nome, int Ordem, int X, int Y, int Largura, int Altura);
}
