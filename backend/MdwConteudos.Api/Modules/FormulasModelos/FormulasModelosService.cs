using System.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ConversorHtml.Application.Services;
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
    Task<ModeloDetalhe> SaveComposicaoAsync(int modeloId, SalvarComposicaoRequest request, int codUsuario, CancellationToken ct);
    Task<string> PreviewComposicaoAsync(int modeloId, SalvarComposicaoRequest request, int codUsuario, CancellationToken ct);
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
            LEFT JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
            LEFT JOIN VARIAVEIS V ON V.CODVARIAVEL = COALESCE(FV.CODVARIAVEL, F.CODVARIAVEL)
            WHERE {whereSql}", parameters, ct: ct));

        parameters.Add("first", pageSize);
        parameters.Add("skip", skip);
        var rows = await connection.QueryAsync<FormulaListItem>(Cmd($@"
            SELECT FIRST @first SKIP @skip
                F.CODFORMULA AS CodFormula,
                F.CODVARIAVEL AS CodVariavel,
                COALESCE(F.NOME, '') AS Nome,
                COALESCE(F.FORMULA, '') AS Formula,
                F.DESCRICAO AS Descricao,
                COALESCE(F.CASADECIMAIS, 2) AS CasasDecimais,
                COALESCE(LIST(V.NOME, ', '), '') AS Variaveis,
                COALESCE(LIST(V.SIGLA, ', '), '') AS Siglas
            FROM FORMULAS F
            LEFT JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
            LEFT JOIN VARIAVEIS V ON V.CODVARIAVEL = COALESCE(FV.CODVARIAVEL, F.CODVARIAVEL)
            WHERE {whereSql}
            GROUP BY F.CODFORMULA, F.CODVARIAVEL, F.NOME, F.FORMULA, F.DESCRICAO, F.CASADECIMAIS
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
                   FROM FORMULAS F
                   LEFT JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
                  WHERE F.CODVARIAVEL = V.CODVARIAVEL
                     OR (F.CODVARIAVEL IS NULL AND FV.CODVARIAVEL = V.CODVARIAVEL)
                  ORDER BY CASE WHEN F.CODVARIAVEL = V.CODVARIAVEL THEN 0 ELSE 1 END) AS Formula,
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
                   CODVARIAVEL AS CodVariavel,
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
        if (formula.CodVariavel is > 0 && !variavelIds.Contains(formula.CodVariavel.Value))
            variavelIds.Insert(0, formula.CodVariavel.Value);

        var equacoes = (await connection.QueryAsync<EquacaoDto>(Cmd(@"
            SELECT EL.CODLINGUAGEM AS CodLinguagem,
                   COALESCE(L.NOME, '') AS Linguagem,
                   EL.CODREFERENCIA AS CodReferencia,
                   CASE WHEN R.CODREFERENCIA IS NULL THEN NULL
                        ELSE R.TITULO || COALESCE(' (' || CAST(R.ANO AS VARCHAR(10)) || ')', '')
                   END AS Referencia,
                   COALESCE(EL.EQUACAO, '') AS Equacao,
                   EL.NOME_FUNCAO AS NomeFuncao
            FROM EQUACOESLINGUAGEM EL
            JOIN TIPOLINGUAGEM L ON L.CODLINGUAGEM = EL.CODLINGUAGEM
            LEFT JOIN REFERENCIA R ON R.CODREFERENCIA = EL.CODREFERENCIA
            WHERE EL.CODFORMULA = @id
            ORDER BY L.NOME, EL.CODEQUACAO", new { id }, ct: ct))).AsList();

        return new FormulaDetalhe(
            formula.CodFormula,
            formula.CodVariavel,
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
            var ownerId = ResolveOwnerVariableId(request, variableIds);
            var owner = variables.First(x => x.CodVariavel == ownerId);
            var name = string.IsNullOrWhiteSpace(request.Nome) ? owner.Sigla : request.Nome.Trim();
            var description = string.IsNullOrWhiteSpace(request.Descricao)
                ? $"Fórmula para {owner.Nome}"
                : request.Descricao.Trim();

            var id = await connection.ExecuteScalarAsync<int>(Cmd(@"
                INSERT INTO FORMULAS (NOME, DESCRICAO, FORMULA, CASADECIMAIS, DTHRULTMODIFICACAO, CODUSUARIO, CODVARIAVEL)
                VALUES (@name, @description, @formula, @decimalPlaces, @now, @codUsuario, @ownerId)
                RETURNING CODFORMULA", new
            {
                name,
                description,
                formula = request.Formula.Trim(),
                decimalPlaces = request.CasasDecimais,
                now = DateTime.Now,
                codUsuario,
                ownerId
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
            var ownerId = ResolveOwnerVariableId(request, variableIds);
            var owner = variables.First(x => x.CodVariavel == ownerId);
            var name = string.IsNullOrWhiteSpace(request.Nome) ? owner.Sigla : request.Nome.Trim();
            await connection.ExecuteAsync(Cmd(@"
                UPDATE FORMULAS
                   SET NOME = @name,
                       DESCRICAO = @description,
                       FORMULA = @formula,
                       CASADECIMAIS = @decimalPlaces,
                       DTHRULTMODIFICACAO = @now,
                       CODUSUARIO = @codUsuario,
                       CODVARIAVEL = @ownerId
                 WHERE CODFORMULA = @id", new
            {
                id,
                name,
                description = NullIfWhiteSpace(request.Descricao),
                formula = request.Formula.Trim(),
                decimalPlaces = request.CasasDecimais,
                now = DateTime.Now,
                codUsuario,
                ownerId
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

            await connection.ExecuteAsync(Cmd("DELETE FROM EQUACOESLINGUAGEM WHERE CODFORMULA = @id", new { id }, transaction, ct));
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
            SELECT COUNT(*) FROM MODELOMODOTEXTO M
            WHERE M.CODUSUARIO = @codUsuario {filter}", parameters, ct: ct));

        parameters.Add("first", pageSize);
        parameters.Add("skip", (page - 1) * pageSize);
        var items = (await connection.QueryAsync<ModeloListItem>(Cmd($@"
            SELECT FIRST @first SKIP @skip
                   M.CODMODELO AS CodModelo,
                   COALESCE(M.NOME, '') AS Nome,
                   CAST((SELECT COUNT(*) FROM SECAOMODOTEXTO S WHERE S.CODMODELO = M.CODMODELO) AS INTEGER) AS TotalSecoes
            FROM MODELOMODOTEXTO M
            WHERE M.CODUSUARIO = @codUsuario {filter}
            ORDER BY M.NOME, M.CODMODELO", parameters, ct: ct))).AsList();

        return Page(items, page, pageSize, total);
    }

    public async Task<ModeloDetalhe?> GetModeloAsync(int id, int codUsuario, CancellationToken ct)
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        var model = await connection.QueryFirstOrDefaultAsync<ModeloRow>(Cmd(@"
            SELECT CODMODELO AS CodModelo, COALESCE(NOME, '') AS Nome
            FROM MODELOMODOTEXTO
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
            FROM SECAOMODOTEXTO
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
                         WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS Normalidade,
                       (SELECT FIRST 1 COALESCE(EL.EQUACAO, F.FORMULA)
                          FROM FORMULAS F
                          LEFT JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
                          LEFT JOIN EQUACOESLINGUAGEM EL ON EL.CODFORMULA = F.CODFORMULA
                          LEFT JOIN TIPOLINGUAGEM TL ON TL.CODLINGUAGEM = EL.CODLINGUAGEM
                         WHERE F.CODVARIAVEL = V.CODVARIAVEL
                            OR (F.CODVARIAVEL IS NULL AND FV.CODVARIAVEL = V.CODVARIAVEL)
                         ORDER BY CASE WHEN F.CODVARIAVEL = V.CODVARIAVEL THEN 0 ELSE 1 END,
                                  CASE WHEN COALESCE(TL.NOME, '') CONTAINING 'javascript' THEN 0 ELSE 1 END,
                                  CASE WHEN EL.EQUACAO IS NULL THEN 1 ELSE 0 END) AS Formula,
                       (SELECT LIST(COALESCE(N.SEXO, '-') || '|' ||
                                           COALESCE(CAST(N.VALORMIN AS VARCHAR(30)), '') || '|' ||
                                           COALESCE(CAST(N.VALORMAX AS VARCHAR(30)), '') || '|' ||
                                           COALESCE(C.NOME, ''), ';')
                          FROM NORMALIDADE N
                          LEFT JOIN CLASSIFICACOES C ON C.CODCLASSIFICACAO = N.CODCLASSIFICACAO
                         WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS NormalidadeDetalhes
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
    {
        await using var connection = await _connections.OpenConnectionAsync(ct);
        var rows = await LoadCatalogRowsAsync(connection, null, ct);

        var tokenMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            AddVariableToken(tokenMap, row.Sigla, row.CodVariavel);
            AddVariableToken(tokenMap, row.Codigo, row.CodVariavel);
        }
        var knownTokens = tokenMap.Keys.ToArray();

        return rows.Select(row =>
        {
            var dependencies = ModeloModoTextoRules.ExtractDependencyTokens(row.Formula, knownTokens);
            var ids = dependencies.Where(tokenMap.ContainsKey).Select(x => tokenMap[x]).Where(x => x != row.CodVariavel).Distinct().ToArray();
            var missing = dependencies.Where(x => !tokenMap.ContainsKey(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return new VariavelOpcao(row.CodVariavel, row.Nome, row.Sigla, row.Formula, row.Normalidade,
                row.Unidade, row.Abreviacao, row.CodGrupo, row.Grupo, row.CasasDecimais,
                row.Classificacoes, ids, missing);
        }).ToList();
    }

    public async Task<int> CreateModeloAsync(ModeloUpsertRequest request, int codUsuario, CancellationToken ct)
    {
        var name = RequiredName(request.Nome, "O nome do modelo é obrigatório.");
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        if (await connection.ExecuteScalarAsync<int>(Cmd(@"
                SELECT COUNT(*) FROM MODELOMODOTEXTO
                WHERE UPPER(NOME) = UPPER(@name) AND CODUSUARIO = @codUsuario",
                new { name, codUsuario }, transaction, ct)) > 0)
            throw new InvalidOperationException("Já existe um modelo com esse nome.");

        var id = await connection.ExecuteScalarAsync<int>(Cmd(
            "SELECT COALESCE(MAX(CODMODELO), 0) + 1 FROM MODELOMODOTEXTO",
            transaction: transaction, ct: ct));
        await connection.ExecuteAsync(Cmd(@"
            INSERT INTO MODELOMODOTEXTO (CODMODELO, NOME, CODUSUARIO)
            VALUES (@id, @name, @codUsuario)", new { id, name, codUsuario }, transaction, ct));
        transaction.Commit();
        return id;
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
                    SELECT COUNT(*) FROM MODELOMODOTEXTO
                    WHERE UPPER(NOME) = UPPER(@name) AND CODUSUARIO = @codUsuario AND CODMODELO <> @id",
                    new { name, codUsuario, id }, transaction, ct)) > 0)
                throw new InvalidOperationException("Já existe um modelo com esse nome.");

            await connection.ExecuteAsync(Cmd(@"
                UPDATE MODELOMODOTEXTO
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
                WHERE CODSECAO IN (SELECT CODSECAO FROM SECAOMODOTEXTO WHERE CODMODELO = @id)",
                new { id }, transaction, ct));
            await connection.ExecuteAsync(Cmd("DELETE FROM SECAOMODOTEXTO WHERE CODMODELO = @id", new { id }, transaction, ct));
            await connection.ExecuteAsync(Cmd(
                "DELETE FROM MODELOMODOTEXTO WHERE CODMODELO = @id AND CODUSUARIO = @codUsuario",
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
                SELECT COALESCE(MAX(ORDEM), 0) + 1 FROM SECAOMODOTEXTO WHERE CODMODELO = @modeloId",
                new { modeloId }, transaction, ct));
            var sectionId = await connection.ExecuteScalarAsync<int>(Cmd(
                "SELECT COALESCE(MAX(CODSECAO), 0) + 1 FROM SECAOMODOTEXTO",
                transaction: transaction, ct: ct));
            await connection.ExecuteAsync(Cmd(@"
                INSERT INTO SECAOMODOTEXTO (CODSECAO, NOME, CODMODELO, ORDEM)
                VALUES (@sectionId, @name, @modeloId, @order)", new { sectionId, name, modeloId, order }, transaction, ct));
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
                UPDATE SECAOMODOTEXTO SET NOME = @name
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
                DELETE FROM SECAOMODOTEXTO WHERE CODSECAO = @secaoId AND CODMODELO = @modeloId",
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
                "SELECT CODSECAO FROM SECAOMODOTEXTO WHERE CODMODELO = @modeloId",
                new { modeloId }, transaction, ct))).OrderBy(x => x).ToArray();
            if (!existing.SequenceEqual(ids.OrderBy(x => x)))
                throw new InvalidOperationException("A ordenação deve conter todas as seções do modelo, sem repetições.");

            for (var i = 0; i < ids.Count; i++)
                await connection.ExecuteAsync(Cmd(@"
                    UPDATE SECAOMODOTEXTO SET ORDEM = @order
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
                "SELECT CODSECAO FROM SECAOMODOTEXTO WHERE CODMODELO = @modeloId",
                new { modeloId }, transaction, ct))).OrderBy(x => x).ToArray();
            if (!existing.SequenceEqual(layout.Select(x => x.CodSecao).OrderBy(x => x)))
                throw new InvalidOperationException("O layout deve conter todas as seções do modelo.");

            foreach (var item in layout)
                await connection.ExecuteAsync(Cmd(@"
                    UPDATE SECAOMODOTEXTO
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

    public async Task<ModeloDetalhe> SaveComposicaoAsync(
        int modeloId,
        SalvarComposicaoRequest request,
        int codUsuario,
        CancellationToken ct)
    {
        var name = RequiredName(request.Nome, "O nome do modelo é obrigatório.");
        var sections = NormalizeComposition(request.Secoes);
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();
        try
        {
            await EnsureModelOwnership(connection, transaction, modeloId, codUsuario, ct);
            var existingIds = (await connection.QueryAsync<int>(Cmd(
                "SELECT CODSECAO FROM SECAOMODOTEXTO WHERE CODMODELO = @modeloId",
                new { modeloId }, transaction, ct))).ToHashSet();
            var requestedIds = sections.Where(x => x.CodSecao is > 0).Select(x => x.CodSecao!.Value).ToArray();
            if (requestedIds.Distinct().Count() != requestedIds.Length || requestedIds.Any(x => !existingIds.Contains(x)))
                throw new InvalidOperationException("A composição contém uma seção inválida ou repetida.");

            var catalog = await LoadCatalogRowsAsync(connection, transaction, ct);
            ValidateCompositionDependencies(sections, catalog);

            if (await connection.ExecuteScalarAsync<int>(Cmd(@"
                    SELECT COUNT(*) FROM MODELOMODOTEXTO
                    WHERE UPPER(NOME) = UPPER(@name) AND CODUSUARIO = @codUsuario AND CODMODELO <> @modeloId",
                    new { name, codUsuario, modeloId }, transaction, ct)) > 0)
                throw new InvalidOperationException("Já existe um modelo com esse nome.");

            await connection.ExecuteAsync(Cmd(@"
                UPDATE MODELOMODOTEXTO SET NOME = @name, DTHRULTMODIFICACAO = @now
                WHERE CODMODELO = @modeloId AND CODUSUARIO = @codUsuario",
                new { name, now = DateTime.Now, modeloId, codUsuario }, transaction, ct));

            var removedIds = existingIds.Except(requestedIds).ToArray();
            if (removedIds.Length > 0)
            {
                await connection.ExecuteAsync(Cmd("DELETE FROM SECAO_VARIAVEL WHERE CODSECAO IN @removedIds", new { removedIds }, transaction, ct));
                await connection.ExecuteAsync(Cmd("DELETE FROM SECAOMODOTEXTO WHERE CODSECAO IN @removedIds AND CODMODELO = @modeloId", new { removedIds, modeloId }, transaction, ct));
            }

            var globalOrder = 0;
            var nextSectionId = await connection.ExecuteScalarAsync<int>(Cmd(
                "SELECT COALESCE(MAX(CODSECAO), 0) + 1 FROM SECAOMODOTEXTO",
                transaction: transaction, ct: ct));
            foreach (var section in sections.OrderBy(x => x.Coluna).ThenBy(x => x.Ordem))
            {
                globalOrder++;
                var x = section.Coluna == 1 ? 0 : 6;
                var y = Math.Max(0, section.Ordem - 1);
                int sectionId;
                if (section.CodSecao is > 0)
                {
                    sectionId = section.CodSecao.Value;
                    await connection.ExecuteAsync(Cmd(@"
                        UPDATE SECAOMODOTEXTO
                           SET NOME = @sectionName, ORDEM = @globalOrder, POSICAO_X = @x,
                               POSICAO_Y = @y, LARGURA = 6, ALTURA = 4
                         WHERE CODSECAO = @sectionId AND CODMODELO = @modeloId",
                        new { sectionName = section.Nome, globalOrder, x, y, sectionId, modeloId }, transaction, ct));
                    await connection.ExecuteAsync(Cmd("DELETE FROM SECAO_VARIAVEL WHERE CODSECAO = @sectionId", new { sectionId }, transaction, ct));
                }
                else
                {
                    sectionId = nextSectionId++;
                    await connection.ExecuteAsync(Cmd(@"
                        INSERT INTO SECAOMODOTEXTO
                            (CODSECAO, NOME, CODMODELO, ORDEM, POSICAO_X, POSICAO_Y, LARGURA, ALTURA)
                        VALUES (@sectionId, @sectionName, @modeloId, @globalOrder, @x, @y, 6, 4)",
                        new { sectionId, sectionName = section.Nome, modeloId, globalOrder, x, y }, transaction, ct));
                }

                var variables = section.Variaveis.Select((v, index) =>
                    new SecaoVariavelRequest(v.CodVariavel, v.ExibirGrafico, index + 1)).ToArray();
                await InsertSectionVariables(connection, transaction, sectionId, variables, ct);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return await GetModeloAsync(modeloId, codUsuario, ct)
            ?? throw new InvalidOperationException("Modelo não encontrado após salvar a composição.");
    }

    public async Task<string> PreviewComposicaoAsync(
        int modeloId,
        SalvarComposicaoRequest request,
        int codUsuario,
        CancellationToken ct)
    {
        var name = RequiredName(request.Nome, "O nome do modelo é obrigatório.");
        var sections = NormalizeComposition(request.Secoes);
        await using var connection = await _connections.OpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        await EnsureModelOwnership(connection, transaction, modeloId, codUsuario, ct);
        var catalog = await LoadCatalogRowsAsync(connection, transaction, ct);
        ValidateCompositionDependencies(sections, catalog);
        transaction.Commit();
        return GenerateText(BuildDraftModel(modeloId, name, sections, catalog));
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

    // Uma variável só pode pertencer a uma fórmula (FORMULAS.CODVARIAVEL é único).
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
            ? @"SELECT COUNT(*) FROM (
                    SELECT CODFORMULA FROM FORMULA_VARIAVEL WHERE CODVARIAVEL IN @ids AND CODFORMULA <> @currentFormulaId
                    UNION
                    SELECT CODFORMULA FROM FORMULAS WHERE CODVARIAVEL IN @ids AND CODFORMULA <> @currentFormulaId
                ) X"
            : @"SELECT COUNT(*) FROM (
                    SELECT CODFORMULA FROM FORMULA_VARIAVEL WHERE CODVARIAVEL IN @ids
                    UNION
                    SELECT CODFORMULA FROM FORMULAS WHERE CODVARIAVEL IN @ids
                ) X";
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

        await connection.ExecuteAsync(Cmd("DELETE FROM EQUACOESLINGUAGEM WHERE CODFORMULA = @formulaId", new { formulaId }, transaction, ct));
        await connection.ExecuteAsync(Cmd("DELETE FROM FORMULA_VARIAVEL WHERE CODFORMULA = @formulaId", new { formulaId }, transaction, ct));
        foreach (var variableId in variableIds)
            await connection.ExecuteAsync(Cmd(@"
                INSERT INTO FORMULA_VARIAVEL (CODFORMULA, CODVARIAVEL, CODUSUARIO, DTHRULTMODIFICACAO)
                VALUES (@formulaId, @variableId, @codUsuario, @now)",
                new { formulaId, variableId, codUsuario, now = DateTime.Now }, transaction, ct));

        foreach (var equation in equations)
            await connection.ExecuteAsync(Cmd(@"
                INSERT INTO EQUACOESLINGUAGEM (CODFORMULA, CODLINGUAGEM, CODREFERENCIA, EQUACAO, NOME_FUNCAO)
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
                SELECT COUNT(*) FROM MODELOMODOTEXTO
                WHERE CODMODELO = @modelId AND CODUSUARIO = @userId",
                new { modelId, userId }, transaction, ct)) == 0)
            throw new InvalidOperationException("Modelo não encontrado ou sem permissão de acesso.");
    }

    private static async Task EnsureSectionOwnership(FbConnection connection, IDbTransaction transaction, int modelId, int sectionId, int userId, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(Cmd(@"
                SELECT COUNT(*)
                FROM SECAOMODOTEXTO S
                JOIN MODELOMODOTEXTO M ON M.CODMODELO = S.CODMODELO
                WHERE S.CODSECAO = @sectionId AND S.CODMODELO = @modelId AND M.CODUSUARIO = @userId",
                new { sectionId, modelId, userId }, transaction, ct)) == 0)
            throw new InvalidOperationException("Seção não encontrada ou sem permissão de acesso.");
    }

    private static async Task EnsureSectionNameAvailable(FbConnection connection, IDbTransaction transaction, int modelId, int? sectionId, string name, CancellationToken ct)
    {
        var sql = sectionId.HasValue
            ? @"SELECT COUNT(*) FROM SECAOMODOTEXTO
                WHERE CODMODELO = @modelId AND UPPER(NOME) = UPPER(@name) AND CODSECAO <> @sectionId"
            : @"SELECT COUNT(*) FROM SECAOMODOTEXTO
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
            SELECT CODSECAO FROM SECAOMODOTEXTO
            WHERE CODMODELO = @modelId ORDER BY ORDEM, NOME",
            new { modelId }, transaction, ct))).AsList();
        for (var i = 0; i < ids.Count; i++)
            await connection.ExecuteAsync(Cmd(
                "UPDATE SECAOMODOTEXTO SET ORDEM = @order WHERE CODSECAO = @id",
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

    private static IReadOnlyList<NormalizedCompositionSection> NormalizeComposition(IReadOnlyList<ComposicaoSecaoRequest>? sections)
    {
        var result = (sections ?? []).Select(section =>
        {
            var name = RequiredName(section.Nome, "Toda seção deve possuir um nome.");
            if (section.Coluna is not (1 or 2))
                throw new InvalidOperationException("A coluna da seção deve ser 1 ou 2.");
            if (section.Ordem <= 0)
                throw new InvalidOperationException("A ordem da seção deve ser maior que zero.");
            var variables = (section.Variaveis ?? [])
                .OrderBy(x => x.Ordem)
                .ToArray();
            if (variables.Any(x => x.CodVariavel <= 0) || variables.Select(x => x.CodVariavel).Distinct().Count() != variables.Length)
                throw new InvalidOperationException($"A seção '{name}' contém medidas inválidas ou repetidas.");
            return new NormalizedCompositionSection(section.CodSecao, name, section.Coluna, section.Ordem, variables);
        }).ToArray();

        if (result.GroupBy(x => x.Nome, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Não é permitido repetir o nome de uma seção.");
        if (result.GroupBy(x => new { x.Coluna, x.Ordem }).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Duas seções não podem ocupar a mesma posição.");
        if (result.SelectMany(x => x.Variaveis).GroupBy(x => x.CodVariavel).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Uma medida só pode aparecer uma vez no modelo.");
        return result;
    }

    private static async Task<IReadOnlyList<CatalogVariableRow>> LoadCatalogRowsAsync(
        FbConnection connection,
        IDbTransaction? transaction,
        CancellationToken ct)
        => (await connection.QueryAsync<CatalogVariableRow>(Cmd(@"
            SELECT V.CODVARIAVEL AS CodVariavel,
                   COALESCE(V.NOME, '') AS Nome,
                   COALESCE(V.SIGLA, '') AS Sigla,
                   V.VARIAVEL AS Codigo,
                   V.ABREVIACAO AS Abreviacao,
                   V.CODGRUPO AS CodGrupo,
                   G.NOME AS Grupo,
                   U.DESCRICAO AS Unidade,
                   COALESCE(V.CASASDECIMAIS, 2) AS CasasDecimais,
                   (SELECT FIRST 1 COALESCE(EL.EQUACAO, F.FORMULA)
                      FROM FORMULAS F
                      LEFT JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
                      LEFT JOIN EQUACOESLINGUAGEM EL ON EL.CODFORMULA = F.CODFORMULA
                      LEFT JOIN TIPOLINGUAGEM TL ON TL.CODLINGUAGEM = EL.CODLINGUAGEM
                     WHERE F.CODVARIAVEL = V.CODVARIAVEL
                        OR (F.CODVARIAVEL IS NULL AND FV.CODVARIAVEL = V.CODVARIAVEL)
                     ORDER BY CASE WHEN F.CODVARIAVEL = V.CODVARIAVEL THEN 0 ELSE 1 END,
                              CASE WHEN COALESCE(TL.NOME, '') CONTAINING 'javascript' THEN 0 ELSE 1 END,
                              CASE WHEN EL.EQUACAO IS NULL THEN 1 ELSE 0 END) AS Formula,
                   (SELECT LIST(COALESCE(N.SEXO, '-') || ': ' || COALESCE(CAST(N.VALORMIN AS VARCHAR(30)), '-') || ' a ' || COALESCE(CAST(N.VALORMAX AS VARCHAR(30)), '-'), '; ')
                      FROM NORMALIDADE N WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS Normalidade,
                   (SELECT LIST(DISTINCT C.NOME, ', ')
                      FROM NORMALIDADE N
                      JOIN CLASSIFICACOES C ON C.CODCLASSIFICACAO = N.CODCLASSIFICACAO
                     WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS Classificacoes,
                   (SELECT LIST(COALESCE(N.SEXO, '-') || '|' ||
                                       COALESCE(CAST(N.VALORMIN AS VARCHAR(30)), '') || '|' ||
                                       COALESCE(CAST(N.VALORMAX AS VARCHAR(30)), '') || '|' ||
                                       COALESCE(C.NOME, ''), ';')
                      FROM NORMALIDADE N
                      LEFT JOIN CLASSIFICACOES C ON C.CODCLASSIFICACAO = N.CODCLASSIFICACAO
                     WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS NormalidadeDetalhes
              FROM VARIAVEIS V
              LEFT JOIN GRUPOSVARIAVEIS G ON G.CODGRUPO = V.CODGRUPO
              LEFT JOIN UNIDADEMEDIDA U ON U.CODUNIDADEMEDIDA = V.CODUNIDADEMEDIDA
             ORDER BY COALESCE(G.NOME, ''), V.NOME, V.SIGLA", null, transaction, ct))).AsList();

    private static void ValidateCompositionDependencies(
        IReadOnlyList<NormalizedCompositionSection> sections,
        IReadOnlyList<CatalogVariableRow> catalog)
    {
        var byId = catalog.ToDictionary(x => x.CodVariavel);
        var selected = sections.SelectMany(x => x.Variaveis).Select(x => x.CodVariavel).ToHashSet();
        if (selected.Any(x => !byId.ContainsKey(x)))
            throw new InvalidOperationException("Uma ou mais medidas selecionadas não existem.");

        var tokenMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in catalog)
        {
            AddVariableToken(tokenMap, variable.Sigla, variable.CodVariavel);
            AddVariableToken(tokenMap, variable.Codigo, variable.CodVariavel);
        }
        var knownTokens = tokenMap.Keys.ToArray();
        var graph = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var variableId in selected)
        {
            var variable = byId[variableId];
            var tokens = ModeloModoTextoRules.ExtractDependencyTokens(variable.Formula, knownTokens);
            var missing = tokens.Where(x => !tokenMap.ContainsKey(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"A fórmula de '{variable.Nome}' referencia medidas inexistentes: {string.Join(", ", missing)}.");
            var dependencies = tokens.Select(x => tokenMap[x]).Where(x => x != variableId).Distinct().ToArray();
            var absent = dependencies.Where(x => !selected.Contains(x)).Select(x => byId[x].Nome).ToArray();
            if (absent.Length > 0)
                throw new InvalidOperationException($"Inclua as dependências de '{variable.Nome}': {string.Join(", ", absent)}.");
            graph[variableId] = dependencies.Where(selected.Contains).ToArray();
        }

        _ = ModeloModoTextoRules.ResolveDependencyOrder(graph.Keys, graph);
    }

    private static ModeloDetalhe BuildDraftModel(
        int modelId,
        string name,
        IReadOnlyList<NormalizedCompositionSection> sections,
        IReadOnlyList<CatalogVariableRow> catalog)
    {
        var byId = catalog.ToDictionary(x => x.CodVariavel);
        var sectionDtos = sections.OrderBy(x => x.Coluna).ThenBy(x => x.Ordem).Select((section, index) =>
            new SecaoDto(
                section.CodSecao ?? -(index + 1), section.Nome, index + 1,
                section.Coluna == 1 ? 0 : 6, section.Ordem - 1, 6, 4,
                section.Variaveis.Select((item, itemIndex) =>
                {
                    var variable = byId[item.CodVariavel];
                    return new SecaoVariavelDto(variable.CodVariavel, variable.Nome, variable.Sigla,
                        item.ExibirGrafico, itemIndex + 1, variable.Unidade, variable.Normalidade,
                        variable.Formula, variable.NormalidadeDetalhes);
                }).ToArray())).ToArray();
        return new ModeloDetalhe(modelId, name, sectionDtos);
    }

    private static void AddVariableToken(IDictionary<string, int> map, string? value, int id)
    {
        var token = NormalizeVariableToken(value ?? "");
        if (!string.Equals(token, "CAMPO", StringComparison.OrdinalIgnoreCase)) map.TryAdd(token, id);
    }

    private static string GenerateText(ModeloDetalhe model)
    {
        static List<string> SectionLines(SecaoDto section, IReadOnlyList<string> knownTokens)
        {
            var lines = new List<string> { $"[{section.Nome.ToUpperInvariant()}]" };
            foreach (var variable in section.Variaveis.OrderBy(x => x.Ordem))
            {
                var normality = BuildNormalitySuffix(variable.NormalidadeDetalhes, variable.Normalidade);
                var code = ModoTextoCodigoFormatter.ToCodigoSuffix(variable.Formula, knownTokens);
                lines.Add($"{variable.Nome} ({NormalizeVariableToken(variable.Sigla)}): 0.0  {variable.Unidade ?? "sem unidade"}{normality}{code}");
            }
            lines.Add("");
            return lines;
        }

        var knownTokens = model.Secoes.SelectMany(x => x.Variaveis).Select(x => NormalizeVariableToken(x.Sigla)).ToArray();
        var left = model.Secoes.Where(x => x.X < 6).OrderBy(x => x.Y).ThenBy(x => x.Ordem).SelectMany(x => SectionLines(x, knownTokens)).ToList();
        var right = model.Secoes.Where(x => x.X >= 6).OrderBy(x => x.Y).ThenBy(x => x.Ordem).SelectMany(x => SectionLines(x, knownTokens)).ToList();
        return ModeloModoTextoRules.ComposeTwoColumns(left, right);
    }

    private static string BuildNormalitySuffix(string? details, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(details))
            return string.IsNullOrWhiteSpace(fallback) ? "" : " " + string.Join(" ", fallback.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => $"({x})"));
        var rows = details.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('|')).Where(x => x.Length >= 4).ToArray();
        var simple = rows.Where(x => string.IsNullOrWhiteSpace(x[3])).Select(x => $"({x[0]}: {x[1]} a {x[2]})").ToArray();
        var classified = rows.Where(x => !string.IsNullOrWhiteSpace(x[3]))
            .Select(x => $"({x[0]}: " + "{" + $"{x[1]}, {x[2]}, {MdwConteudos.Api.Infrastructure.NormalidadeZonas.MapColor(x[3])} " + "})").ToArray();
        var result = simple.Length == 0 ? "" : " " + string.Join(" ", simple);
        if (classified.Length > 0) result += "  Comentário: " + string.Join(',', classified);
        return result;
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

    private static int ResolveOwnerVariableId(FormulaUpsertRequest request, IReadOnlyList<int> variableIds)
    {
        if (request.CodVariavel is > 0)
        {
            if (!variableIds.Contains(request.CodVariavel.Value))
                throw new InvalidOperationException("CODVARIAVEL deve estar entre as variáveis vinculadas.");
            return request.CodVariavel.Value;
        }

        return variableIds[0];
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

    private sealed record FormulaRow(int CodFormula, int? CodVariavel, string Nome, string Formula, string? Descricao, int CasasDecimais);
    private sealed record VariableRow(int CodVariavel, string Nome, string Sigla);
    private sealed record ModeloRow(int CodModelo, string Nome);
    private sealed record SectionRow(int CodSecao, string Nome, int Ordem, int X, int Y, int Largura, int Altura);
    private sealed record CatalogVariableRow(
        int CodVariavel,
        string Nome,
        string Sigla,
        string? Codigo,
        string? Abreviacao,
        int? CodGrupo,
        string? Grupo,
        string? Unidade,
        int CasasDecimais,
        string? Formula,
        string? Normalidade,
        string? Classificacoes,
        string? NormalidadeDetalhes);
    private sealed record NormalizedCompositionSection(
        int? CodSecao,
        string Nome,
        int Coluna,
        int Ordem,
        IReadOnlyList<ComposicaoVariavelRequest> Variaveis);
}
