using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Services;

public sealed record ImportarNormalidadesJsonRequest(int CodReferencia, string? Fonte);

public interface INormalidadesJsonImportService
{
    Task<object> ImportarAseChamberAsync(int codReferencia, int codUsuario, CancellationToken ct);
}

public sealed class NormalidadesJsonImportService : INormalidadesJsonImportService
{
    public const string GrupoGravidadePt = "Gravidade eco (PT)";
    public const string FonteAseChamber = "Recommendations for Cardiac Chamber Quantification by Echocardiography in Adults";

    private static readonly string[] ZonasPt = ["normal", "leve", "moderado", "grave"];
    private static readonly string[] SexosJson = ["F", "M", "U", "A"];

    private readonly IFirebirdConnectionFactory _db;
    private readonly string _migrationRoot;

    public NormalidadesJsonImportService(
        IFirebirdConnectionFactory db,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _db = db;
        _migrationRoot = MigrationRootResolver.ResolveMigrationRoot(config, env.ContentRootPath);
    }

    public async Task<object> ImportarAseChamberAsync(int codReferencia, int codUsuario, CancellationToken ct)
    {
        if (codReferencia <= 0)
            throw new InvalidOperationException("Informe uma referência válida.");

        var jsonPath = ResolveJsonPath();
        if (!File.Exists(jsonPath))
            throw new InvalidOperationException($"Arquivo JSON não encontrado: {jsonPath}");

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var titulo = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT TITULO FROM REFERENCIA WHERE CODREFERENCIA = @cod",
            new { cod = codReferencia }, tx);
        if (titulo is null && await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM REFERENCIA WHERE CODREFERENCIA = @cod",
                new { cod = codReferencia }, tx) == 0)
            throw new InvalidOperationException($"Referência {codReferencia} não encontrada.");
        var tituloOk = !string.IsNullOrWhiteSpace(titulo)
            && titulo.Contains("Chamber Quantification", StringComparison.OrdinalIgnoreCase);

        var classificacoes = await EnsureGrupoPtAsync(conn, tx, codUsuario);
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath, ct));

        var variaveisAtualizadas = 0;
        var faixasInseridas = 0;
        var comentariosGravados = 0;
        var naoEncontradas = new List<string>();
        var ignoradasFonte = 0;
        var zonasIgnoradas = 0;

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!prop.Value.ValueKind.Equals(JsonValueKind.Object)) continue;
            var fonte = ReadFonte(prop.Value);
            if (!IsAseChamber(fonte))
            {
                ignoradasFonte++;
                continue;
            }

            var codigo = prop.Name.Trim();
            var alternativasJson = ReadAlternativas(prop.Value);
            var codVariavel = await ResolveCodVariavelAsync(conn, tx, codigo, alternativasJson);
            if (!codVariavel.HasValue)
            {
                naoEncontradas.Add(codigo);
                continue;
            }

            var pagina = ReadPagina(prop.Value);
            await conn.ExecuteAsync(
                "DELETE FROM NORMALIDADE WHERE CODVARIAVEL = @codVariavel AND CODREFERENCIA = @codReferencia",
                new { codVariavel = codVariavel.Value, codReferencia }, tx);

            foreach (var sexoJson in SexosJson)
            {
                if (!prop.Value.TryGetProperty(sexoJson, out var sexoNode) || sexoNode.ValueKind != JsonValueKind.Object)
                    continue;
                var sexo = MapSexo(sexoJson);
                if (sexo is null) continue;

                foreach (var zona in ZonasPt)
                {
                    if (!sexoNode.TryGetProperty(zona, out var faixa) || faixa.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!TryReadRange(faixa, out var min, out var max))
                    {
                        zonasIgnoradas++;
                        continue;
                    }

                    if (!classificacoes.TryGetValue(zona, out var codClassificacao))
                    {
                        zonasIgnoradas++;
                        continue;
                    }

                    await conn.ExecuteAsync(
                        @"
                        INSERT INTO NORMALIDADE (
                            CODVARIAVEL, CODREFERENCIA, CODCLASSIFICACAO, SEXO,
                            VALORMIN, VALORMAX, PAGINA_REFERENCIA,
                            CODUSUARIO, DTHRULTMODIFICACAO
                        ) VALUES (
                            @CodVariavel, @CodReferencia, @CodClassificacao, @Sexo,
                            @ValorMin, @ValorMax, @Pagina,
                            @CodUsuario, @Now
                        )",
                        new
                        {
                            CodVariavel = codVariavel.Value,
                            CodReferencia = codReferencia,
                            CodClassificacao = codClassificacao,
                            Sexo = sexo,
                            ValorMin = min,
                            ValorMax = max,
                            Pagina = pagina,
                            CodUsuario = codUsuario,
                            Now = DateTime.Now
                        },
                        tx);
                    faixasInseridas++;
                }
            }

            await ReplaceClassificacoesVariavelAsync(conn, tx, codVariavel.Value, classificacoes.Values, codUsuario);

            var comentario = ReadComentario(prop.Value);
            if (!string.IsNullOrWhiteSpace(comentario))
            {
                var texto = Iso88591SafeText.ForStorage(comentario.Trim());
                if (texto.Length > 500) texto = texto[..500];
                await conn.ExecuteAsync(
                    @"
                    UPDATE OR INSERT INTO NORMALIDADECOMENTARIO
                        (CODVARIAVEL, CODREFERENCIA, SEXO, IDADE_MIN, IDADE_MAX, TEXTO, CODUSUARIO, DTHRULTMODIFICACAO)
                    VALUES
                        (@CodVariavel, @CodReferencia, 'A', -1, -1, @Texto, @CodUsuario, @Now)
                    MATCHING (CODVARIAVEL, CODREFERENCIA, SEXO, IDADE_MIN, IDADE_MAX)",
                    new
                    {
                        CodVariavel = codVariavel.Value,
                        CodReferencia = codReferencia,
                        Texto = texto,
                        CodUsuario = codUsuario,
                        Now = DateTime.Now
                    },
                    tx);
                comentariosGravados++;
            }
            else
            {
                await conn.ExecuteAsync(
                    "DELETE FROM NORMALIDADECOMENTARIO WHERE CODVARIAVEL = @codVariavel AND CODREFERENCIA = @codReferencia",
                    new { codVariavel = codVariavel.Value, codReferencia }, tx);
            }

            variaveisAtualizadas++;
        }

        await tx.CommitAsync(ct);
        return new
        {
            codReferencia,
            tituloReferencia = titulo,
            tituloCompativel = tituloOk,
            fonteFiltro = FonteAseChamber,
            arquivo = jsonPath,
            variaveisAtualizadas,
            faixasInseridas,
            comentariosGravados,
            ignoradasFonte,
            zonasIgnoradas,
            naoEncontradas,
            message = $"{variaveisAtualizadas} variável(is) atualizada(s), {faixasInseridas} faixa(s), {comentariosGravados} comentário(s). {naoEncontradas.Count} não encontrada(s) no banco."
        };
    }

    private string ResolveJsonPath()
    {
        return Path.GetFullPath(Path.Combine(_migrationRoot, "docs", "normalidades", "NormalidadesEcodopplercardiograma.json"));
    }

    private static async Task<Dictionary<string, int>> EnsureGrupoPtAsync(
        FbConnection conn,
        IDbTransaction tx,
        int codUsuario)
    {
        var grupoId = await conn.ExecuteScalarAsync<int?>(
            "SELECT CODGRUPO FROM GRUPOSCLASSIFICACOES WHERE UPPER(NOME) = UPPER(@nome)",
            new { nome = GrupoGravidadePt }, tx);

        if (!grupoId.HasValue)
        {
            grupoId = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO GRUPOSCLASSIFICACOES (NOME, CODUSUARIO, DTHRULTMODIFICACAO)
                  VALUES (@nome, @codUsuario, @now)
                  RETURNING CODGRUPO",
                new { nome = GrupoGravidadePt, codUsuario, now = DateTime.Now }, tx);
        }

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (zona, nome) in new[] { ("normal", "Normal"), ("leve", "Leve"), ("moderado", "Moderado"), ("grave", "Grave") })
        {
            var id = await conn.ExecuteScalarAsync<int?>(
                "SELECT FIRST 1 CODCLASSIFICACAO FROM CLASSIFICACOES WHERE UPPER(NOME) = UPPER(@nome)",
                new { nome }, tx);
            if (!id.HasValue)
            {
                id = await conn.ExecuteScalarAsync<int>(
                    @"INSERT INTO CLASSIFICACOES (NOME, CODGRUPO, CODUSUARIO, DTHRULTMODIFICACAO)
                      VALUES (@nome, @grupo, @codUsuario, @now)
                      RETURNING CODCLASSIFICACAO",
                    new { nome, grupo = grupoId.Value, codUsuario, now = DateTime.Now }, tx);
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE CLASSIFICACOES SET CODGRUPO = @grupo WHERE CODCLASSIFICACAO = @id AND (CODGRUPO IS NULL OR CODGRUPO <> @grupo)",
                    new { grupo = grupoId.Value, id = id.Value }, tx);
            }
            map[zona] = id.Value;
        }

        return map;
    }

    private static async Task<int?> ResolveCodVariavelAsync(
        FbConnection conn,
        IDbTransaction tx,
        string codigo,
        IReadOnlyList<string> alternativasJson)
    {
        var candidates = new List<string> { codigo };
        candidates.AddRange(alternativasJson);
        foreach (var name in candidates.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var id = await conn.ExecuteScalarAsync<int?>(
                "SELECT FIRST 1 CODVARIAVEL FROM VARIAVEIS WHERE UPPER(VARIAVEL) = UPPER(@name)",
                new { name }, tx);
            if (id.HasValue) return id;

            id = await conn.ExecuteScalarAsync<int?>(
                "SELECT FIRST 1 CODVARIAVEL FROM VARIAVEISALTERNATIVAS WHERE UPPER(ALTERNATIVA) = UPPER(@name)",
                new { name }, tx);
            if (id.HasValue) return id;
        }

        return null;
    }

    private static async Task ReplaceClassificacoesVariavelAsync(
        FbConnection conn,
        IDbTransaction tx,
        int codVariavel,
        IEnumerable<int> classificacoesPt,
        int codUsuario)
    {
        await conn.ExecuteAsync(
            "DELETE FROM VARIAVEIS_CLASSIFICACOES WHERE CODVARIAVEL = @codVariavel",
            new { codVariavel }, tx);
        foreach (var code in classificacoesPt.Distinct())
        {
            await conn.ExecuteAsync(
                @"INSERT INTO VARIAVEIS_CLASSIFICACOES (CODVARIAVEL, CODCLASSIFICACAO, CODUSUARIO, DTHRULTMODIFICACAO)
                  VALUES (@codVariavel, @code, @codUsuario, @now)",
                new { codVariavel, code, codUsuario, now = DateTime.Now }, tx);
        }
    }

    /// <summary>CHK_NORMALIDADE_SEXO aceita só M, F e A (ambos). O JSON usa U para unissex.</summary>
    private static string? MapSexo(string? raw)
    {
        var s = (raw ?? "").Trim().ToUpperInvariant();
        return s switch
        {
            "M" or "MASCULINO" => "M",
            "F" or "FEMININO" => "F",
            "A" or "U" or "AMBOS" or "UNISSEX" or "UNISEX" => "A",
            _ => null
        };
    }

    private static bool IsAseChamber(string? fonte) =>
        !string.IsNullOrWhiteSpace(fonte)
        && fonte.Contains("Chamber Quantification", StringComparison.OrdinalIgnoreCase);

    private static string? ReadFonte(JsonElement node) =>
        node.TryGetProperty("_meta", out var meta) && meta.TryGetProperty("Fonte", out var fonte)
            ? fonte.GetString()
            : null;

    private static int? ReadPagina(JsonElement node)
    {
        if (!node.TryGetProperty("_meta", out var meta) || !meta.TryGetProperty("Pagina", out var pagina))
            return null;
        if (pagina.ValueKind == JsonValueKind.Number && pagina.TryGetInt32(out var i)) return i;
        if (pagina.ValueKind == JsonValueKind.Number && pagina.TryGetDouble(out var d)) return (int)d;
        return int.TryParse(pagina.GetString(), out var parsed) ? parsed : null;
    }

    private static string? ReadComentario(JsonElement node)
    {
        if (!node.TryGetProperty("COMENTARIOTEXTO", out var bloco)) return null;
        if (bloco.ValueKind == JsonValueKind.Object && bloco.TryGetProperty("comentario", out var c))
            return c.GetString();
        return bloco.ValueKind == JsonValueKind.String ? bloco.GetString() : null;
    }

    private static List<string> ReadAlternativas(JsonElement node)
    {
        var list = new List<string>();
        if (!node.TryGetProperty("VARIAVEISALTERNATIVAS", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var item in arr.EnumerateArray())
        {
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value)) list.Add(value.Trim());
        }
        return list;
    }

    private static bool TryReadRange(JsonElement faixa, out double min, out double max)
    {
        min = 0;
        max = 0;
        if (!faixa.TryGetProperty("min", out var minEl) || !faixa.TryGetProperty("max", out var maxEl))
            return false;
        return TryReadNumber(minEl, out min) && TryReadNumber(maxEl, out max);
    }

    private static bool TryReadNumber(JsonElement el, out double value)
    {
        if (el.ValueKind == JsonValueKind.Number)
        {
            value = el.GetDouble();
            return true;
        }

        return double.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
