using System.Data.Common;
using System.Globalization;
using System.Text;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Web;

public interface IImportacaoVariaveisService
{
    Task<ImportacaoVariaveisPreview> PreviewAsync(IFormFile arquivo, int? codReferencia, CancellationToken ct);
    Task<ImportacaoVariaveisResultado> ConfirmarAsync(IFormFile arquivo, IReadOnlyList<ImportacaoVariavelDecisao> decisoes, int? codReferencia, int codUsuario, CancellationToken ct);
}

public sealed class ImportacaoVariaveisService(IFirebirdConnectionFactory db) : IImportacaoVariaveisService
{
    public const long MaxFileSize = 10L * 1024 * 1024;
    private const string FormatoNormalidades = "json-normalidades-medware";
    private static readonly HashSet<string> AcoesValidas = new(["criar", "atualizar", "alternativa", "ignorar"], StringComparer.OrdinalIgnoreCase);

    public async Task<ImportacaoVariaveisPreview> PreviewAsync(IFormFile arquivo, int? codReferencia, CancellationToken ct)
    {
        var parsed = await ParseAsync(arquivo, ct);
        await using var conn = await db.OpenConnectionAsync(ct);
        return await EnrichAsync(conn, null, ApplyReference(parsed, codReferencia), ct);
    }

    public async Task<ImportacaoVariaveisResultado> ConfirmarAsync(
        IFormFile arquivo, IReadOnlyList<ImportacaoVariavelDecisao> decisoes, int? codReferencia,
        int codUsuario, CancellationToken ct)
    {
        var normalizedDecisions = decisoes
            .Where(item => !string.IsNullOrWhiteSpace(item.Codigo))
            .GroupBy(item => item.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last() with { Codigo = group.Key, Acao = group.Last().Acao.Trim().ToLowerInvariant() })
            .ToList();
        if (normalizedDecisions.Count == 0 || normalizedDecisions.All(item => item.Acao == "ignorar"))
            throw new InvalidOperationException("Selecione ao menos uma variável para criar, atualizar ou vincular como alternativa.");
        if (normalizedDecisions.Any(item => !AcoesValidas.Contains(item.Acao)))
            throw new InvalidOperationException("A seleção contém uma ação de importação inválida.");

        var parsed = await ParseAsync(arquivo, ct);
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var preview = await EnrichAsync(conn, tx, ApplyReference(parsed, codReferencia), ct);
            var available = preview.Variaveis.ToDictionary(item => item.Codigo, StringComparer.OrdinalIgnoreCase);
            var unknown = normalizedDecisions.Where(item => !available.ContainsKey(item.Codigo)).Select(item => item.Codigo).ToList();
            if (unknown.Count > 0) throw new InvalidOperationException($"Variáveis não encontradas no arquivo: {string.Join(", ", unknown)}.");

            var inserted = 0;
            var updated = 0;
            var ignored = normalizedDecisions.Count(item => item.Acao == "ignorar");
            var formulasInserted = 0;
            var normalsInserted = 0;
            var aliasesInserted = 0;
            var commentsInserted = 0;
            var classifications = await LoadClassificationsAsync(conn, tx, ct);

            foreach (var decision in normalizedDecisions.Where(item => item.Acao != "ignorar"))
            {
                var item = available[decision.Codigo];
                var targetId = decision.Acao switch
                {
                    "atualizar" => item.CodVariavelExistente,
                    "alternativa" => decision.CodVariavelPrincipal,
                    _ => null
                };

                if (decision.Acao == "criar" && item.ExisteNoBanco)
                {
                    ignored++;
                    continue;
                }
                if (decision.Acao == "atualizar" && preview.Formato != FormatoNormalidades)
                    throw new InvalidOperationException($"{item.Codigo}: a atualização de variáveis existentes só é aceita no JSON de normalidades Medware.");
                if (decision.Acao is "atualizar" or "alternativa")
                {
                    if (!targetId.HasValue || await VariableExistsAsync(conn, tx, targetId.Value, ct) == 0)
                        throw new InvalidOperationException($"{item.Codigo}: selecione uma variável principal válida.");
                }
                var importsDependencies = decision.Acao is "criar" or "atualizar"
                    || decision.Acao == "alternativa" && preview.Formato == FormatoNormalidades;
                if (importsDependencies && !item.Valido)
                    throw new InvalidOperationException($"{item.Codigo}: {string.Join(" ", item.Erros)}");

                if (decision.Acao == "criar")
                {
                    var unit = await EnsureUnitAsync(conn, tx, item.Unidade, codUsuario, ct);
                    if (await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                            "SELECT COUNT(*) FROM VARIAVEIS WHERE UPPER(VARIAVEL)=UPPER(@Codigo)", item, tx, cancellationToken: ct)) > 0)
                    {
                        ignored++;
                        continue;
                    }
                    targetId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                        "INSERT INTO VARIAVEIS (NOME,VARIAVEL,SIGLA,ABREVIACAO,DESCRICAO,CODUNIDADEMEDIDA,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@Nome,@Codigo,@Sigla,@Abreviacao,'',@unit,@codUsuario,@now) RETURNING CODVARIAVEL",
                        new { item.Nome, item.Codigo, item.Sigla, item.Abreviacao, unit, codUsuario, now = DateTime.Now }, tx, cancellationToken: ct));
                    inserted++;
                }
                else if (decision.Acao == "atualizar")
                {
                    updated++;
                }

                if (!targetId.HasValue) throw new InvalidOperationException($"{item.Codigo}: não foi possível determinar a variável de destino.");

                // Os aliases declarados no arquivo são pistas para a sugestão de similaridade.
                // O código importado só vira alias quando o administrador escolhe essa ação explicitamente.
                var aliases = decision.Acao == "alternativa" ? new[] { item.Codigo } : [];
                foreach (var alias in aliases)
                    if (await InsertAliasAsync(conn, tx, targetId.Value, alias, codUsuario, ct)) aliasesInserted++;

                if (!importsDependencies) continue;

                foreach (var formula in preview.Formulas.Where(formula => Related(formula.Variavel, item)))
                {
                    if (!formula.Valido) throw new InvalidOperationException($"Fórmula inválida para {item.Codigo}: {string.Join(" ", formula.Erros)}");
                    var formulaId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                        "INSERT INTO FORMULAS (NOME,DESCRICAO,FORMULA,CASADECIMAIS,CODUSUARIO,DTHRULTMODIFICACAO,CODVARIAVEL) VALUES (@Sigla,@description,@Expressao,@CasasDecimais,@codUsuario,@now,@targetId) RETURNING CODFORMULA",
                        new { item.Sigla, description = $"Fórmula para {item.Sigla}", formula.Expressao, formula.CasasDecimais, codUsuario, now = DateTime.Now, targetId }, tx, cancellationToken: ct));
                    await conn.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO FORMULA_VARIAVEL (CODFORMULA,CODVARIAVEL,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@formulaId,@targetId,@codUsuario,@now)",
                        new { formulaId, targetId, codUsuario, now = DateTime.Now }, tx, cancellationToken: ct));
                    formulasInserted++;
                }

                var itemNormals = preview.Normalidades.Where(normal => Related(normal.Variavel, item)).ToList();
                if (preview.Formato == FormatoNormalidades)
                {
                    foreach (var referenceId in itemNormals.Select(normal => normal.CodReferencia).Where(id => id.HasValue).Select(id => id!.Value).Distinct())
                        await conn.ExecuteAsync(new CommandDefinition(
                            "DELETE FROM NORMALIDADE WHERE CODVARIAVEL=@targetId AND CODREFERENCIA=@referenceId",
                            new { targetId, referenceId }, tx, cancellationToken: ct));
                }

                foreach (var normal in itemNormals)
                {
                    if (!normal.Valido || !normal.CodReferencia.HasValue)
                        throw new InvalidOperationException($"Normalidade inválida para {item.Codigo}: {string.Join(" ", normal.Erros)}");
                    int? classificationId = null;
                    if (!string.IsNullOrWhiteSpace(normal.Classificacao))
                    {
                        if (!classifications.TryGetValue(normal.Classificacao, out var foundClassification))
                            throw new InvalidOperationException($"Classificação '{normal.Classificacao}' não encontrada no banco.");
                        classificationId = foundClassification;
                    }
                    await conn.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO NORMALIDADE (CODVARIAVEL,CODREFERENCIA,CODCLASSIFICACAO,VALORMIN,VALORMAX,SEXO,IDADE_MIN,IDADE_MAX,PAGINA_REFERENCIA,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@targetId,@CodReferencia,@classificationId,@ValorMin,@ValorMax,@Sexo,@IdadeMin,@IdadeMax,@Pagina,@codUsuario,@now)",
                        new { targetId, normal.CodReferencia, classificationId, normal.ValorMin, normal.ValorMax, normal.Sexo, normal.IdadeMin, normal.IdadeMax, normal.Pagina, codUsuario, now = DateTime.Now }, tx, cancellationToken: ct));
                    normalsInserted++;
                    if (classificationId.HasValue)
                        await conn.ExecuteAsync(new CommandDefinition(
                            "UPDATE OR INSERT INTO VARIAVEIS_CLASSIFICACOES (CODVARIAVEL,CODCLASSIFICACAO,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@targetId,@classificationId,@codUsuario,@now) MATCHING (CODVARIAVEL,CODCLASSIFICACAO)",
                            new { targetId, classificationId, codUsuario, now = DateTime.Now }, tx, cancellationToken: ct));
                }

                foreach (var comment in itemNormals
                             .Where(normal => normal.CodReferencia.HasValue && !string.IsNullOrWhiteSpace(normal.Comentario))
                             .GroupBy(normal => new { normal.CodReferencia, Texto = normal.Comentario!.Trim() })
                             .Select(group => group.Key))
                {
                    await conn.ExecuteAsync(new CommandDefinition(
                        "UPDATE OR INSERT INTO NORMALIDADECOMENTARIO (CODVARIAVEL,CODREFERENCIA,TEXTO,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@targetId,@CodReferencia,@Texto,@codUsuario,@now) MATCHING (CODVARIAVEL,CODREFERENCIA)",
                        new { targetId, comment.CodReferencia, comment.Texto, codUsuario, now = DateTime.Now }, tx, cancellationToken: ct));
                    commentsInserted++;
                }
            }

            await tx.CommitAsync(ct);
            return new(inserted, ignored, formulasInserted, normalsInserted, [], aliasesInserted, updated, commentsInserted);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<ImportacaoVariaveisPreview> EnrichAsync(DbConnection conn, DbTransaction? tx, ImportacaoVariaveisPreview parsed, CancellationToken ct)
    {
        var references = (await conn.QueryAsync<ReferenceRow>(new CommandDefinition(
            "SELECT CODREFERENCIA AS CodReferencia,TITULO AS Titulo,ANO AS Ano FROM REFERENCIA",
            transaction: tx, cancellationToken: ct))).ToList();
        var normals = new List<ImportacaoNormalidadeItem>();
        foreach (var normal in parsed.Normalidades)
        {
            int? referenceId = normal.CodReferencia;
            var errors = normal.Erros.ToList();
            if (referenceId.HasValue && references.All(reference => reference.CodReferencia != referenceId.Value)) referenceId = null;
            if (!referenceId.HasValue && !string.IsNullOrWhiteSpace(normal.Referencia))
            {
                var title = normal.Referencia.Trim();
                var matches = references.Where(reference =>
                    (!normal.AnoReferencia.HasValue || reference.Ano == normal.AnoReferencia)
                    && reference.Titulo.Equals(title, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 0)
                {
                    var normalizedTitle = Normalize(title);
                    matches = references.Where(reference =>
                        (!normal.AnoReferencia.HasValue || reference.Ano == normal.AnoReferencia)
                        && (Normalize(reference.Titulo).Contains(normalizedTitle, StringComparison.Ordinal)
                            || normalizedTitle.Contains(Normalize(reference.Titulo), StringComparison.Ordinal))).ToList();
                }
                if (matches.Count == 1) referenceId = matches[0].CodReferencia;
                else if (matches.Count > 1) errors.Add("Há mais de uma referência compatível; informe o código.");
            }
            errors.RemoveAll(error => error is "Referência não localizada." or "Selecione uma referência para as normalidades do JSON Studio.");
            if (!referenceId.HasValue) errors.Add("Referência não localizada.");
            normals.Add(normal with
            {
                CodReferencia = referenceId,
                ReferenciaValida = referenceId.HasValue,
                Valido = errors.Count == 0,
                Erros = errors.Distinct().ToList()
            });
        }

        var existing = (await conn.QueryAsync<ExistingVariableRow>(new CommandDefinition(
            "SELECT CODVARIAVEL AS CodVariavel,VARIAVEL AS Codigo,NOME AS Nome,SIGLA AS Sigla,ABREVIACAO AS Abreviacao FROM VARIAVEIS",
            transaction: tx, cancellationToken: ct))).ToList();
        var alternatives = (await conn.QueryAsync<ExistingAlternativeRow>(new CommandDefinition(
            "SELECT CODVARIAVEL AS CodVariavel,ALTERNATIVA AS Alternativa FROM VARIAVEISALTERNATIVAS",
            transaction: tx, cancellationToken: ct))).ToList();
        var alternativesByVariable = alternatives.GroupBy(item => item.CodVariavel).ToDictionary(group => group.Key, group => group.Select(item => item.Alternativa).ToList());
        var variables = new List<ImportacaoVariavelItem>();
        foreach (var item in parsed.Variaveis)
        {
            var exact = existing.FirstOrDefault(candidate => candidate.Codigo.Equals(item.Codigo, StringComparison.OrdinalIgnoreCase));
            var errors = item.Erros.ToList();
            errors.AddRange(parsed.Formulas.Where(formula => Related(formula.Variavel, item) && !formula.Valido).SelectMany(formula => formula.Erros));
            errors.AddRange(normals.Where(normal => Related(normal.Variavel, item) && !normal.Valido).SelectMany(normal => normal.Erros));
            variables.Add(item with
            {
                ExisteNoBanco = exact is not null,
                CodVariavelExistente = exact?.CodVariavel,
                Sugestoes = FindSuggestions(item, existing, alternativesByVariable),
                Valido = errors.Count == 0,
                Erros = errors.Distinct().ToList()
            });
        }
        return parsed with { Variaveis = variables, Normalidades = normals };
    }

    private static IReadOnlyList<ImportacaoVariavelSugestao> FindSuggestions(
        ImportacaoVariavelItem item, IReadOnlyList<ExistingVariableRow> existing,
        IReadOnlyDictionary<int, List<string>> alternatives)
    {
        var importedNames = new[] { item.Codigo, item.Nome, item.Sigla, item.Abreviacao }
            .Concat(item.AlternativasArquivo ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => NormalizeVariableName(value!))
            .Where(value => value.Length >= 2).Distinct().ToList();
        var scored = new List<ImportacaoVariavelSugestao>();
        foreach (var candidate in existing)
        {
            if (candidate.Codigo.Equals(item.Codigo, StringComparison.OrdinalIgnoreCase)) continue;
            var candidateNames = new[] { candidate.Codigo, candidate.Nome, candidate.Sigla, candidate.Abreviacao }
                .Concat(alternatives.TryGetValue(candidate.CodVariavel, out var aliases) ? aliases : [])
                .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => NormalizeVariableName(value!))
                .Where(value => value.Length >= 2).Distinct().ToList();
            var score = 0;
            var reason = "Nome semelhante";
            foreach (var imported in importedNames)
            foreach (var current in candidateNames)
            {
                var currentScore = Similarity(imported, current);
                if (currentScore <= score) continue;
                score = currentScore;
                reason = score == 100 ? "Código ou nome equivalente" : imported.Contains(current) || current.Contains(imported) ? "Código ou nome relacionado" : "Nome semelhante";
            }
            if (score >= 62) scored.Add(new(candidate.CodVariavel, candidate.Codigo, candidate.Nome, candidate.Sigla, score, reason));
        }
        return scored.OrderByDescending(item => item.Pontuacao).ThenBy(item => item.Codigo).Take(5).ToList();
    }

    private static int Similarity(string left, string right)
    {
        if (left == right) return 100;
        if (left.Length >= 3 && right.Length >= 3 && (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal)))
            return 88 - Math.Min(20, Math.Abs(left.Length - right.Length) * 2);
        var distance = Levenshtein(left, right);
        return (int)Math.Round((1d - (double)distance / Math.Max(left.Length, right.Length)) * 100d);
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            previous = current;
        }
        return previous[right.Length];
    }

    private static string NormalizeVariableName(string value)
    {
        var normalized = Normalize(value).Replace("variavel", "", StringComparison.Ordinal);
        return normalized.StartsWith("vr", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        return builder.ToString();
    }

    private static async Task<int> EnsureUnitAsync(DbConnection conn, DbTransaction tx, string unitDescription, int codUsuario, CancellationToken ct)
    {
        var unit = await conn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT FIRST 1 CODUNIDADEMEDIDA FROM UNIDADEMEDIDA WHERE UPPER(DESCRICAO)=UPPER(@unitDescription)",
            new { unitDescription }, tx, cancellationToken: ct));
        return unit ?? await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "INSERT INTO UNIDADEMEDIDA (DESCRICAO,STATUS,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@unitDescription,-1,@codUsuario,@now) RETURNING CODUNIDADEMEDIDA",
            new { unitDescription, codUsuario, now = DateTime.Now }, tx, cancellationToken: ct));
    }

    private static Task<int> VariableExistsAsync(DbConnection conn, DbTransaction tx, int codVariavel, CancellationToken ct) =>
        conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE CODVARIAVEL=@codVariavel", new { codVariavel }, tx, cancellationToken: ct));

    private static async Task<bool> InsertAliasAsync(DbConnection conn, DbTransaction tx, int targetId, string rawAlias, int codUsuario, CancellationToken ct)
    {
        var alias = rawAlias.Trim();
        if (string.IsNullOrWhiteSpace(alias)) return false;
        var principal = await conn.QueryFirstOrDefaultAsync<ExistingVariableRow>(new CommandDefinition(
            "SELECT CODVARIAVEL AS CodVariavel,VARIAVEL AS Codigo,NOME AS Nome,SIGLA AS Sigla,ABREVIACAO AS Abreviacao FROM VARIAVEIS WHERE UPPER(VARIAVEL)=UPPER(@alias)",
            new { alias }, tx, cancellationToken: ct));
        if (principal is not null)
        {
            if (principal.CodVariavel == targetId) return false;
            throw new InvalidOperationException($"O código alternativo '{alias}' já é o código principal da variável {principal.Codigo}.");
        }
        var linkedId = await conn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT FIRST 1 CODVARIAVEL FROM VARIAVEISALTERNATIVAS WHERE UPPER(ALTERNATIVA)=UPPER(@alias)",
            new { alias }, tx, cancellationToken: ct));
        if (linkedId.HasValue)
        {
            if (linkedId.Value == targetId) return false;
            throw new InvalidOperationException($"A alternativa '{alias}' já está vinculada a outra variável principal.");
        }
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO VARIAVEISALTERNATIVAS (CODVARIAVEL,ALTERNATIVA,CODUSUARIO,DTHRULTMODIFICACAO) VALUES (@targetId,@alias,@codUsuario,@now)",
            new { targetId, alias, codUsuario, now = DateTime.Now }, tx, cancellationToken: ct));
        return true;
    }

    private static async Task<Dictionary<string, int>> LoadClassificationsAsync(DbConnection conn, DbTransaction tx, CancellationToken ct)
    {
        var rows = await conn.QueryAsync<ClassificationRow>(new CommandDefinition(
            "SELECT CODCLASSIFICACAO AS CodClassificacao,NOME AS Nome FROM CLASSIFICACOES",
            transaction: tx, cancellationToken: ct));
        return rows.GroupBy(row => row.Nome, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().CodClassificacao, StringComparer.OrdinalIgnoreCase);
    }

    private static bool Related(string relation, ImportacaoVariavelItem item) =>
        relation.Equals(item.Codigo, StringComparison.OrdinalIgnoreCase)
        || relation.Equals(item.Sigla, StringComparison.OrdinalIgnoreCase)
        || relation.Equals($"VR_{item.Sigla}", StringComparison.OrdinalIgnoreCase);

    private static ImportacaoVariaveisPreview ApplyReference(ImportacaoVariaveisPreview parsed, int? codReferencia) =>
        !codReferencia.HasValue ? parsed : parsed with
        {
            Normalidades = parsed.Normalidades.Select(normal => normal.CodReferencia.HasValue ? normal : normal with
            {
                CodReferencia = codReferencia,
                Erros = normal.Erros.Where(error => !error.Contains("Selecione uma referência", StringComparison.OrdinalIgnoreCase)).ToList()
            }).ToList()
        };

    private static async Task<ImportacaoVariaveisPreview> ParseAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new InvalidOperationException("Selecione um arquivo não vazio.");
        if (file.Length > MaxFileSize) throw new InvalidOperationException("O arquivo excede o limite de 10 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".json" or ".cs")) throw new InvalidOperationException("Apenas arquivos .json ou .cs são permitidos.");
        using var reader = new StreamReader(file.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
        var source = await reader.ReadToEndAsync(ct);
        var parsed = extension == ".json"
            ? JsonVariaveisParser.Parse(source, Path.GetFileName(file.FileName))
            : CSharpVariaveisParser.Parse(source, Path.GetFileName(file.FileName));
        if (parsed.Variaveis.Count == 0) throw new InvalidOperationException("Nenhuma variável encontrada no arquivo.");
        return parsed;
    }

    private sealed record ExistingVariableRow(int CodVariavel, string Codigo, string? Nome, string? Sigla, string? Abreviacao);
    private sealed record ExistingAlternativeRow(int CodVariavel, string Alternativa);
    private sealed record ReferenceRow(int CodReferencia, string Titulo, int? Ano);
    private sealed record ClassificationRow(int CodClassificacao, string Nome);
}
