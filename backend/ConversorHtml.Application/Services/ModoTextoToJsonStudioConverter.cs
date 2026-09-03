using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ConversorHtml.Application.Services;

/// <summary>
/// Converte TXT modo texto LaudosUX em JSON Studio (<c>{ camposScript: [...] }</c>),
/// espelhando a lógica de <c>criarEstruturaScript</c> do Script.js.
/// </summary>
public static partial class ModoTextoToJsonStudioConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Convert(string modoTexto)
    {
        var campos = BuildCampos(modoTexto);
        var root = new JsonObject { ["camposScript"] = campos };
        return root.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    public static JsonArray BuildCampos(string modoTexto)
    {
        var linhas = ProcessarLinhas(modoTexto);
        var campos = new JsonArray();
        if (linhas.Count == 0)
        {
            AppendLaudoDescritivo(campos, coluna: -1, linha: 0, ordem: 1);
            return campos;
        }

        var proximoTitulo = EncontrarProximoItem(linhas);
        var coluna = 1;
        var cont = 0;
        var tabIndex = 1;
        var etiquetaNomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var linhaRaw in linhas)
        {
            var linha = CorrigirTextoComAcento(linhaRaw.Trim());
            if (string.IsNullOrWhiteSpace(linha)) continue;

            if (!string.IsNullOrEmpty(proximoTitulo) && linha.Contains(proximoTitulo, StringComparison.Ordinal))
            {
                coluna = 2;
                cont = 0;
            }

            try
            {
                if (EhEtiqueta(linha))
                {
                    campos.Add(CriarEtiqueta(linha, coluna, cont, etiquetaNomes));
                    cont++;
                    continue;
                }

                var lower = linha.ToLowerInvariant();
                if (lower.Contains("única") || lower.Contains("unica")
                    || lower.Contains("múltipla") || lower.Contains("multipla")
                    || lower.Contains("lista"))
                {
                    campos.Add(CriarSelecao(linha, coluna, cont, ref tabIndex));
                    cont++;
                    continue;
                }

                if (linha.Contains(':'))
                {
                    campos.Add(CriarNumero(linha, coluna, cont, ref tabIndex));
                    cont++;
                }
            }
            catch
            {
                // Linha inválida: ignora (mesmo comportamento do Script.js)
            }
        }

        AppendLaudoDescritivo(campos, coluna: -1, linha: cont, ordem: tabIndex);
        return campos;
    }

    private static List<string> ProcessarLinhas(string texto) =>
        (texto ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("HASH", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static string? EncontrarProximoItem(IReadOnlyList<string> linhas)
    {
        if (linhas.Count == 0) return null;
        var mid = Math.Max(0, (int)Math.Floor(linhas.Count / 2d));
        for (var i = mid; i < linhas.Count; i++)
        {
            if (EhEtiqueta(linhas[i])) return linhas[i].Trim();
        }
        return mid < linhas.Count ? linhas[mid].Trim() : null;
    }

    private static bool EhEtiqueta(string linha) =>
        linha.TrimStart().StartsWith('[') && !linha.Contains('=', StringComparison.Ordinal);

    private static string CorrigirTextoComAcento(string texto)
    {
        return texto
            .Replace("comentario", "comentário", StringComparison.OrdinalIgnoreCase)
            .Replace("codigo", "código", StringComparison.OrdinalIgnoreCase)
            .Replace("unica", "única", StringComparison.OrdinalIgnoreCase)
            .Replace("multipla", "múltipla", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject CriarObjetoBase(int coluna, int linha) => new()
    {
        ["alterado"] = false,
        ["coluna"] = coluna,
        ["imprimir"] = true,
        ["ocultarDescricao"] = false,
        ["linha"] = linha,
        ["geraGrafico"] = false,
        ["mostrarComentarios"] = false,
        ["naoExibirForaPadrao"] = false
    };

    private static JsonObject CriarEtiqueta(string linha, int coluna, int cont, HashSet<string> nomesUsados)
    {
        var descricao = linha.Trim().TrimStart('[').TrimEnd(']').Trim();
        var nome = FormatNomeEtiqueta(descricao);
        if (!nomesUsados.Add(nome))
        {
            var suffix = 1;
            while (!nomesUsados.Add($"{nome}_{suffix}")) suffix++;
            nome = $"{nome}_{suffix}";
        }

        var obj = CriarObjetoBase(coluna, cont);
        obj["descricao"] = descricao;
        obj["nome"] = nome;
        obj["tipo"] = "etiqueta";
        obj["ocultarDescricao"] = true;
        return obj;
    }

    private static JsonObject CriarSelecao(string linha, int coluna, int cont, ref int tabIndex)
    {
        var lower = linha.ToLowerInvariant();
        var tipo = lower.Contains("única") || lower.Contains("unica")
            ? "radio"
            : lower.Contains("múltipla") || lower.Contains("multipla")
                ? "check"
                : "combo";

        var marker = tipo switch
        {
            "radio" => "[Única]",
            "check" => "[Múltipla]",
            _ => "[Lista]"
        };

        var cleaned = Regex.Replace(linha, @"\[(Única|Unica|UNICA|Múltipla|Multipla|MULTIPLA|Lista|LISTA)\]", "", RegexOptions.IgnoreCase).Trim();
        var parts = cleaned.Split('=', 2);
        var left = parts[0].Trim();
        var right = parts.Length > 1 ? parts[1].Trim() : "";

        ExtractNomeVariavel(left, out var descricao, out var nome);
        var opcoes = right
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => (JsonNode)o)
            .ToArray();

        var obj = CriarObjetoBase(coluna, cont);
        obj["descricao"] = descricao;
        obj["nome"] = nome;
        obj["tipo"] = tipo;
        obj["opcoes"] = new JsonArray(opcoes);
        obj["vertical"] = "0";
        obj["ordem"] = tabIndex++;
        obj["funcao"] = "";
        obj["valorPadrao"] = "";
        _ = marker;
        return obj;
    }

    private static JsonObject CriarNumero(string linha, int coluna, int cont, ref int tabIndex)
    {
        const string codigoKey = "código:";
        const string comentarioKey = "comentário:";
        var lower = linha.ToLowerInvariant();
        var hasCodigo = lower.Contains(codigoKey, StringComparison.Ordinal);
        var hasComentario = lower.Contains(comentarioKey, StringComparison.Ordinal);

        var funcao = "";
        var comentarioRaw = "";
        string parteCampo;

        if (hasCodigo && hasComentario)
        {
            var idxComentario = lower.IndexOf(comentarioKey, StringComparison.Ordinal);
            var idxCodigo = lower.IndexOf(codigoKey, StringComparison.Ordinal);
            if (idxComentario < idxCodigo)
            {
                parteCampo = linha[..idxComentario].Trim();
                comentarioRaw = linha[(idxComentario + comentarioKey.Length)..idxCodigo].Trim();
                funcao = linha[(idxCodigo + codigoKey.Length)..].Trim();
            }
            else
            {
                parteCampo = linha[..idxCodigo].Trim();
                funcao = linha[(idxCodigo + codigoKey.Length)..idxComentario].Trim();
                comentarioRaw = linha[(idxComentario + comentarioKey.Length)..].Trim();
            }
        }
        else if (hasCodigo)
        {
            var idxCodigo = lower.IndexOf(codigoKey, StringComparison.Ordinal);
            parteCampo = linha[..idxCodigo].Trim();
            funcao = linha[(idxCodigo + codigoKey.Length)..].Trim();
        }
        else if (hasComentario)
        {
            var idxComentario = lower.IndexOf(comentarioKey, StringComparison.Ordinal);
            parteCampo = linha[..idxComentario].Trim();
            comentarioRaw = linha[(idxComentario + comentarioKey.Length)..].Trim();
        }
        else
        {
            parteCampo = linha.Trim();
        }

        var colonIdx = parteCampo.IndexOf(':');
        var left = colonIdx >= 0 ? parteCampo[..colonIdx].Trim() : parteCampo;
        var right = colonIdx >= 0 ? parteCampo[(colonIdx + 1)..].Trim() : "";

        ExtractNomeVariavel(left, out var descricao, out var nome);

        var tokens = TokenizeValueSide(right);
        var valorPadraoTxt = tokens.Count > 0 ? tokens[0] : "0.0";
        var medida = tokens.Count > 1 && !tokens[1].StartsWith('(') ? tokens[1] : "";
        var casasDecimais = ContarCasasDecimais(valorPadraoTxt);

        var referenciaNormalidade = new JsonArray();
        foreach (var token in tokens.Where(t => t.StartsWith('(') && t.EndsWith(')')))
        {
            var inner = token[1..^1].Trim();
            if (TryParseReferencia(inner, medida, out var refObj))
                referenciaNormalidade.Add(refObj);
        }

        var normalidades = ExtrairNormalidades(comentarioRaw, medida);
        var contemMinMax = referenciaNormalidade.Count > 0 || normalidades.Count > 0;

        var obj = CriarObjetoBase(coluna, cont);
        obj["casasDecimais"] = casasDecimais.ToString(CultureInfo.InvariantCulture);
        obj["descricao"] = descricao;
        obj["geraGrafico"] = true;
        obj["desenho"] = contemMinMax ? "-1" : "0";
        obj["medida"] = medida;
        obj["nome"] = nome;
        obj["textoEscondido"] = false;
        obj["ordem"] = tabIndex++.ToString(CultureInfo.InvariantCulture);
        obj["tipo"] = "numero";
        obj["valorPadrao"] = "";
        obj["funcao"] = funcao;
        obj["normalidades"] = normalidades;
        obj["referenciaNormalidade"] = referenciaNormalidade;
        return obj;
    }

    private static void ExtractNomeVariavel(string left, out string descricao, out string nome)
    {
        var match = ParensRegex().Match(left);
        if (match.Success)
        {
            descricao = left[..match.Index].Trim();
            nome = FormatNomeVariavel(match.Groups[1].Value);
        }
        else
        {
            descricao = left.Trim();
            nome = FormatNomeVariavel(descricao);
        }

        if (string.IsNullOrWhiteSpace(descricao)) descricao = nome;
        if (string.IsNullOrWhiteSpace(nome)) nome = "CAMPO";
    }

    private static List<string> TokenizeValueSide(string value)
    {
        var result = new List<string>();
        foreach (Match m in ValueTokenRegex().Matches(value ?? ""))
            result.Add(m.Value);
        return result;
    }

    private static int ContarCasasDecimais(string valor)
    {
        var idx = valor.IndexOf('.');
        if (idx < 0) idx = valor.IndexOf(',');
        if (idx < 0) return 0;
        var decimals = 0;
        for (var i = idx + 1; i < valor.Length && char.IsDigit(valor[i]); i++) decimals++;
        return decimals;
    }

    private static bool TryParseReferencia(string inner, string medida, out JsonObject obj)
    {
        obj = new JsonObject();
        var sexoMatch = Regex.Match(inner, @"^(M|F|A)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
        if (!sexoMatch.Success) return false;

        var sexo = sexoMatch.Groups[1].Value.ToUpperInvariant();
        var range = sexoMatch.Groups[2].Value.Trim();
        string valorMin = "";
        string valorMax = "";

        var between = Regex.Match(range, @"^(-?[\d.,]+)\s*a\s*(-?[\d.,]+)$", RegexOptions.IgnoreCase);
        if (between.Success)
        {
            valorMin = NormalizeNumber(between.Groups[1].Value);
            valorMax = NormalizeNumber(between.Groups[2].Value);
        }
        else
        {
            valorMin = "";
            valorMax = "";
        }

        obj["sexo"] = sexo;
        obj["valorMin"] = valorMin;
        obj["valorMax"] = valorMax;
        obj["unidadeMedida"] = medida;
        obj["valorExtenso"] = range;
        return true;
    }

    private static JsonArray ExtrairNormalidades(string comentario, string medida)
    {
        var result = new JsonArray();
        if (string.IsNullOrWhiteSpace(comentario)) return result;

        foreach (Match block in CommentParenRegex().Matches(comentario))
        {
            var inner = block.Groups[1].Value.Trim();
            var sexoMatch = Regex.Match(inner, @"^(M|F|A)\s*:\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!sexoMatch.Success) continue;

            var sexo = sexoMatch.Groups[1].Value.ToUpperInvariant();
            var body = sexoMatch.Groups[2].Value;
            foreach (Match faixa in BraceRegex().Matches(body))
            {
                var parts = faixa.Groups[1].Value.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (parts.Length < 2) continue;

                var valorMin = NormalizeNumber(parts[0]);
                var valorMax = NormalizeNumber(parts[1]);
                string valorExtenso = "";
                string cor = "";

                if (parts.Length >= 4)
                {
                    valorExtenso = parts[2];
                    cor = parts[^1];
                }
                else if (parts.Length == 3)
                {
                    if (IsColorToken(parts[2])) cor = parts[2];
                    else valorExtenso = parts[2];
                }

                result.Add(new JsonObject
                {
                    ["sexo"] = sexo,
                    ["valorMin"] = valorMin,
                    ["valorMax"] = valorMax,
                    ["unidadeMedida"] = medida,
                    ["valorExtenso"] = valorExtenso,
                    ["cor"] = cor
                });
            }
        }

        return result;
    }

    private static bool IsColorToken(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return v is "verde" or "vermelho" or "amarelo" or "laranja" or "azul"
            or "txtverde" or "txtvermelho" or "txtamarelo" or "txtlaranja" or "txtazul";
    }

    private static string NormalizeNumber(string value)
    {
        var t = (value ?? "").Trim().Replace(',', '.');
        if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return n.ToString(CultureInfo.InvariantCulture);
        return value?.Trim() ?? "";
    }

    private static string FormatNomeEtiqueta(string descricao)
    {
        var normalized = descricao.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch);
        }

        var token = Regex.Replace(sb.ToString().ToUpperInvariant(), @"\s+", "_");
        token = Regex.Replace(token, @"[^\w]", "");
        return string.IsNullOrWhiteSpace(token) ? "SECAO" : token;
    }

    private static string FormatNomeVariavel(string value)
    {
        var token = value.Trim().ToUpperInvariant();
        if (token.StartsWith("VR_", StringComparison.Ordinal)) token = token[3..];
        token = Regex.Replace(token, @"[^A-Z0-9_]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(token) ? "CAMPO" : token;
    }

    private static void AppendLaudoDescritivo(JsonArray campos, int coluna, int linha, int ordem)
    {
        campos.Add(new JsonObject
        {
            ["coluna"] = coluna,
            ["descricao"] = "Laudo",
            ["geraGrafico"] = false,
            ["mostrarComentarios"] = false,
            ["naoExibirForaPadrao"] = false,
            ["ocultarDescricao"] = false,
            ["imprimir"] = true,
            ["nome"] = "LAUDODESCRITIVO",
            ["ordem"] = ordem,
            ["tipo"] = "textoLongo",
            ["textoEscondido"] = false,
            ["funcao"] = "",
            ["linha"] = linha,
            ["valorPadrao"] = "<p><br></p>"
        });
    }

    [GeneratedRegex(@"\(([^)]+)\)\s*$")]
    private static partial Regex ParensRegex();

    [GeneratedRegex(@"(?:\([^)]*\)|[^\s()]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ValueTokenRegex();

    [GeneratedRegex(@"\(([^()]*(?:\{[^}]*\}[^()]*)*)\)")]
    private static partial Regex CommentParenRegex();

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex BraceRegex();
}
