using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;

namespace ConversorHtml.Application.Services;

/// <summary>
/// Provider Mock para desenvolvimento sem Cursor — interpretação heurística de comandos de voz.
/// </summary>
public partial class MockVoiceLaudoService : IVoiceLaudoService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<string> BootstrapFromImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        await imageStream.CopyToAsync(Stream.Null, cancellationToken);
        return BuildSampleCamposScript($"Bootstrap mock de {Path.GetFileNameWithoutExtension(fileName)}");
    }

    public Task<VoiceApplyResult> ApplyUtteranceAsync(
        VoiceSession session,
        string transcript,
        VoiceUtteranceIntent intent = VoiceUtteranceIntent.Build,
        CancellationToken cancellationToken = default)
    {
        // Modelo vazio ou intent de criação: sempre gera o laudo, nunca "edita" o nada.
        if (intent != VoiceUtteranceIntent.Edit || IsEmptyCampos(session.CamposScriptJson))
        {
            return Task.FromResult(BuildFromSpeech(transcript));
        }

        var edited = EditFromSpeech(session.CamposScriptJson, transcript);
        if (edited.Summary.Contains("Nenhuma alteração", StringComparison.OrdinalIgnoreCase)
            || edited.Warnings.Any(w => w.Contains("não reconhecido", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(BuildFromSpeech(transcript));
        }

        return Task.FromResult(edited);
    }

    private static bool IsEmptyCampos(string json)
    {
        try
        {
            var root = ParseRoot(json);
            return (root["camposScript"] as JsonArray)?.Count is null or 0;
        }
        catch
        {
            return true;
        }
    }

    private VoiceApplyResult BuildFromSpeech(string transcript)
    {
        var text = NormalizeStt(transcript);
        var campos = new JsonArray();
        var sectionsAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldsAdded = 0;

        foreach (var def in KnownFieldDefinitions)
        {
            if (!def.Matches(text)) continue;

            EnsureSection(campos, sectionsAdded, def.SectionTitle, def.SectionVar);
            AddNumericField(campos, def.Descricao, def.Nome, def.Unidade, text, def.SectionVar);
            fieldsAdded++;
        }

        if (fieldsAdded == 0)
        {
            var currentSection = "DADOS_GERAIS";
            var hasSection = false;

            foreach (var clause in SplitClauses(text))
            {
                if (TryParseSectionClause(clause, out var sectionLabel))
                {
                    currentSection = ToVarName(sectionLabel);
                    EnsureSection(campos, sectionsAdded, sectionLabel, currentSection);
                    hasSection = true;
                    continue;
                }

                if (!LooksLikeFieldClause(clause)) continue;

                if (!hasSection)
                {
                    EnsureSection(campos, sectionsAdded, "Dados gerais", "DADOS_GERAIS");
                    hasSection = true;
                }

                var (descricao, unidade, nome) = ParseNumericField(clause);
                AddNumericField(campos, descricao, nome, unidade, clause, currentSection);
                fieldsAdded++;
            }
        }

        if (fieldsAdded == 0)
        {
            foreach (Match match in FieldPhrasePattern().Matches(text))
            {
                EnsureSection(campos, sectionsAdded, "Dados gerais", "DADOS_GERAIS");
                var phrase = match.Value.Trim();
                var (descricao, unidade, nome) = ParseNumericField(phrase);
                AddNumericField(campos, descricao, nome, unidade, phrase, "DADOS_GERAIS");
                fieldsAdded++;
            }
        }

        if (fieldsAdded == 0)
        {
            return new VoiceApplyResult(
                "{\"camposScript\":[]}",
                "Não foi possível extrair campos da descrição. Mencione seções e medidas com unidade (ex.: altura em cm, anel aórtico em milímetros).",
                ["Descreva o laudo em linguagem natural, indicando seções e unidades."]);
        }

        var root = new JsonObject { ["camposScript"] = campos };
        var summary = fieldsAdded == 1
            ? "Modelo criado com 1 campo."
            : $"Modelo criado com {fieldsAdded} campos.";

        return new VoiceApplyResult(root.ToJsonString(JsonOptions), summary, []);
    }

    private static void EnsureSection(JsonArray campos, HashSet<string> sectionsAdded, string sectionTitle, string sectionVar)
    {
        if (!sectionsAdded.Add(sectionVar)) return;

        var id = NextId(campos);
        campos.Add(new JsonObject
        {
            ["coluna"] = 1,
            ["imprimir"] = true,
            ["descricao"] = sectionTitle.ToUpperInvariant(),
            ["nome"] = sectionVar,
            ["tipo"] = "etiqueta",
            ["ordem"] = 200 + id,
            ["id"] = id
        });
    }

    private static string NormalizeStt(string transcript)
    {
        var text = transcript.Trim();
        text = Regex.Replace(text, @"\bsessão\b", "seção", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bsessao\b", "secao", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bmilímetro\b", "mm", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bmilimetros\b", "mm", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bmilímetros\b", "mm", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bcentímetro\b", "cm", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bcentimetros\b", "cm", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bcentímetros\b", "cm", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bquilos?\b", "kg", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bquilogramas?\b", "kg", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bvia seda\b", "via de saída do ve", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\banel aust\b", "anel aórtico", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\banel aortico\b", "anel aórtico", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bhorta\b", "aorta", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bseios de valsava\b", "seios de valsalva", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bmenor óptica indexada\b", "menor óptica indexada", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bmenor optica indexada\b", "menor óptica indexada", RegexOptions.IgnoreCase);
        return text;
    }

    private static string ExtractFieldContext(string text, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            var idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var start = Math.Max(0, idx - 20);
            var end = Math.Min(text.Length, idx + keyword.Length + 80);
            return text[start..end];
        }

        return text;
    }

    private sealed record KnownFieldDef(
        string[] Keywords,
        string Descricao,
        string Nome,
        string Unidade,
        string SectionTitle,
        string SectionVar)
    {
        public bool Matches(string text) =>
            Keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly KnownFieldDef[] KnownFieldDefinitions =
    [
        new(["altura"], "Altura", "ALTURA", "cm", "Dados do paciente", "DADOS_DO_PACIENTE"),
        new(["peso"], "Peso", "PESO", "kg", "Dados do paciente", "DADOS_DO_PACIENTE"),
        new(["superfície corporal", "superficie corporal"], "Superfície corporal", "SC", "m²", "Dados do paciente", "DADOS_DO_PACIENTE"),
        new(["via de saída do ve", "via de saida do ve", "via saída do ve"], "Via de saída do VE", "VSVE", "mm", "Câmaras esquerda", "CAMARAS_ESQUERDA"),
        new(["anel aórtico", "anel aortico"], "Anel aórtico", "ANEL_AORTICO", "mm", "Câmaras esquerda", "CAMARAS_ESQUERDA"),
        new(["menor óptica indexada", "menor optica indexada"], "Menor óptica indexada", "AOI", "mm", "Câmaras esquerda", "CAMARAS_ESQUERDA"),
        new(["seios de valsalva", "seio sinotubular"], "Seios de Valsalva", "SEIOS_VALSALVA", "mm", "Câmaras esquerda", "CAMARAS_ESQUERDA"),
        new(["aorta ascendente"], "Aorta ascendente", "AO_ASC", "mm", "Câmaras esquerda", "CAMARAS_ESQUERDA"),
    ];

    private VoiceApplyResult EditFromSpeech(string camposScriptJson, string transcript)
    {
        var root = ParseRoot(camposScriptJson);
        var campos = root["camposScript"] as JsonArray ?? new JsonArray();
        var text = transcript.Trim();
        var warnings = new List<string>();
        var summary = "Comando de edição não reconhecido.";

        if (RemoveFieldPattern().IsMatch(text))
        {
            var fieldName = ExtractFieldName(text);
            var removed = RemoveField(campos, fieldName);
            summary = removed ? $"Removido campo '{fieldName}'." : $"Campo '{fieldName}' não encontrado.";
        }
        else if (AddSectionPattern().IsMatch(text))
        {
            var section = ExtractSectionName(text);
            AddSection(campos, section);
            summary = $"Criada seção '{section}'.";
        }
        else if (ContainsEditAddIntent(text))
        {
            var (descricao, unidade, nome) = ParseNumericField(text);
            AddNumericField(campos, descricao, nome, unidade, text);
            summary = $"Adicionado campo numérico '{descricao}'.";
        }
        else
        {
            warnings.Add("Comando de edição não reconhecido. Use frases como 'remover peso', 'adicionar altura em cm' ou 'criar seção valvas'.");
        }

        root["camposScript"] = campos;
        return new VoiceApplyResult(root.ToJsonString(JsonOptions), summary, warnings);
    }

    private static IEnumerable<string> SplitClauses(string text)
    {
        var parts = Regex.Split(
            text,
            @"\s*(?:,|;|\be\b|\btambém\b|\bdepois\b|\balém disso\b|\bque tenha\b|\bcontendo\b|\bna seção\b|\boutra seção\b|\boutra sessão\b|\bseção\b|\bsecao\b|\bquero\b)\s*",
            RegexOptions.IgnoreCase);

        return parts
            .Select(p => p.Trim())
            .Where(p => p.Length > 2);
    }

    private static bool TryParseSectionClause(string clause, out string sectionLabel)
    {
        var sectionMatch = Regex.Match(
            clause,
            @"(?:seção|secao|sessão|sessao|grupo|parte)\s+(?:com\s+)?(?:os\s+)?(?:as\s+)?(?:de\s+)?(.+)$",
            RegexOptions.IgnoreCase);

        if (sectionMatch.Success)
        {
            sectionLabel = CleanSectionLabel(sectionMatch.Groups[1].Value);
            return sectionLabel.Length > 0;
        }

        if (Regex.IsMatch(clause, @"\bdados (?:do )?paciente\b", RegexOptions.IgnoreCase))
        {
            sectionLabel = "Dados do paciente";
            return true;
        }

        if (Regex.IsMatch(clause, @"\bdados gerais\b", RegexOptions.IgnoreCase))
        {
            sectionLabel = "Dados gerais";
            return true;
        }

        if (Regex.IsMatch(clause, @"\bcâmaras?\s+(?:esquerda|direita|esq|dir)\b", RegexOptions.IgnoreCase))
        {
            sectionLabel = Regex.Match(clause, @"câmaras?\s+(?:esquerda|direita|esq|dir)", RegexOptions.IgnoreCase).Value;
            return true;
        }

        if (Regex.IsMatch(clause, @"\bvalvas?\b", RegexOptions.IgnoreCase))
        {
            sectionLabel = "Valvas";
            return true;
        }

        if (Regex.IsMatch(clause, @"\baorta\b", RegexOptions.IgnoreCase))
        {
            sectionLabel = "Aorta";
            return true;
        }

        sectionLabel = string.Empty;
        return false;
    }

    private static string CleanSectionLabel(string raw)
    {
        var label = raw.Trim().TrimEnd('.');
        label = Regex.Replace(label, @"\bque vão ter\b.*$", "", RegexOptions.IgnoreCase).Trim();
        label = Regex.Replace(label, @"\bvai ter\b.*$", "", RegexOptions.IgnoreCase).Trim();
        label = Regex.Replace(label, @"\bcom as medidas\b.*$", "", RegexOptions.IgnoreCase).Trim();
        return label;
    }

    private static bool LooksLikeFieldClause(string clause) =>
        FieldPhrasePattern().IsMatch(clause)
        || Regex.IsMatch(clause, @"\b(altura|peso|superfície|superficie|anel|aorta|ve|vd|ae|septo|parede|imc|ritmo|valsalva|óptica|optica)\b", RegexOptions.IgnoreCase);

    private static void AddNumericField(JsonArray campos, string descricao, string nome, string unidade, string transcript, string? sectionOverride = null)
    {
        var section = sectionOverride ?? FindLastSection(campos) ?? "DADOS_GERAIS";
        var id = NextId(campos);
        var field = new JsonObject
        {
            ["coluna"] = 1,
            ["imprimir"] = true,
            ["descricao"] = descricao,
            ["nome"] = nome,
            ["tipo"] = "numero",
            ["medida"] = unidade,
            ["ordem"] = id,
            ["id"] = id,
            ["etiquetaPai"] = section
        };

        var mRange = Regex.Match(transcript, @"(?:homem|masculino|m)\s*(\d+(?:[.,]\d+)?)\s*(?:a|até|-)\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
        var fRange = Regex.Match(transcript, @"(?:mulher|feminino|f)\s*(\d+(?:[.,]\d+)?)\s*(?:a|até|-)\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
        var singleRange = Regex.Match(transcript, @"normalidade\s+(\d+(?:[.,]\d+)?)\s*(?:a|até|-)\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);

        if (mRange.Success || fRange.Success || singleRange.Success)
        {
            var refs = new JsonArray();
            if (fRange.Success)
            {
                refs.Add(new JsonObject { ["sexo"] = "F", ["valorMin"] = fRange.Groups[1].Value, ["valorMax"] = fRange.Groups[2].Value, ["unidadeMedida"] = unidade });
            }
            if (mRange.Success)
            {
                refs.Add(new JsonObject { ["sexo"] = "M", ["valorMin"] = mRange.Groups[1].Value, ["valorMax"] = mRange.Groups[2].Value, ["unidadeMedida"] = unidade });
            }
            if (singleRange.Success && !mRange.Success && !fRange.Success)
            {
                var min = singleRange.Groups[1].Value;
                var max = singleRange.Groups[2].Value;
                refs.Add(new JsonObject { ["sexo"] = "M", ["valorMin"] = min, ["valorMax"] = max, ["unidadeMedida"] = unidade });
                refs.Add(new JsonObject { ["sexo"] = "F", ["valorMin"] = min, ["valorMax"] = max, ["unidadeMedida"] = unidade });
            }
            field["referenciaNormalidade"] = refs;
        }

        campos.Add(field);
    }

    public Task<VoiceExportResult> ExportAsync(VoiceSession session, ConversionOutputFormat format, CancellationToken cancellationToken = default)
    {
        var content = format == ConversionOutputFormat.ModoTexto
            ? ExportToModoTexto(session.CamposScriptJson)
            : ExportToHtml(session.CamposScriptJson);

        return Task.FromResult(new VoiceExportResult(content, format));
    }

    private static string? FindLastSection(JsonArray campos)
    {
        for (var i = campos.Count - 1; i >= 0; i--)
        {
            if (campos[i]?["tipo"]?.GetValue<string>() == "etiqueta")
            {
                return campos[i]?["nome"]?.GetValue<string>();
            }
        }

        return null;
    }

    private static int NextId(JsonArray campos)
    {
        var max = 0;
        foreach (var node in campos)
        {
            if (node?["id"]?.GetValue<int>() is int id && id > max) max = id;
        }

        return max + 1;
    }

    private static JsonObject ParseRoot(string json)
    {
        try
        {
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject { ["camposScript"] = new JsonArray() };
        }
        catch
        {
            return new JsonObject { ["camposScript"] = new JsonArray() };
        }
    }

    private static string BuildSampleCamposScript(string title)
    {
        var root = new JsonObject
        {
            ["camposScript"] = new JsonArray
            {
                new JsonObject
                {
                    ["coluna"] = 1,
                    ["imprimir"] = true,
                    ["descricao"] = "DADOS GERAIS",
                    ["nome"] = "DADOS_GERAIS",
                    ["tipo"] = "etiqueta",
                    ["ordem"] = 200,
                    ["id"] = 1
                },
                new JsonObject
                {
                    ["coluna"] = 1,
                    ["imprimir"] = true,
                    ["descricao"] = "Altura",
                    ["nome"] = "ALTURA",
                    ["tipo"] = "numero",
                    ["medida"] = "cm",
                    ["ordem"] = 1,
                    ["id"] = 2,
                    ["etiquetaPai"] = "DADOS_GERAIS"
                },
                new JsonObject
                {
                    ["coluna"] = 1,
                    ["imprimir"] = true,
                    ["descricao"] = "Peso",
                    ["nome"] = "PESO",
                    ["tipo"] = "numero",
                    ["medida"] = "kg",
                    ["ordem"] = 2,
                    ["id"] = 3,
                    ["etiquetaPai"] = "DADOS_GERAIS"
                }
            }
        };

        return root.ToJsonString(JsonOptions);
    }

    private static bool RemoveField(JsonArray campos, string fieldName)
    {
        for (var i = campos.Count - 1; i >= 0; i--)
        {
            var node = campos[i]?.AsObject();
            if (node is null) continue;
            var nome = node["nome"]?.GetValue<string>() ?? "";
            var desc = node["descricao"]?.GetValue<string>() ?? "";
            if (nome.Contains(fieldName, StringComparison.OrdinalIgnoreCase)
                || desc.Contains(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                campos.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private static void AddSection(JsonArray campos, string sectionName)
    {
        var id = NextId(campos);
        campos.Add(new JsonObject
        {
            ["coluna"] = 1,
            ["imprimir"] = true,
            ["descricao"] = sectionName.ToUpperInvariant(),
            ["nome"] = ToVarName(sectionName),
            ["tipo"] = "etiqueta",
            ["ordem"] = 200 + id,
            ["id"] = id
        });
    }

    private static (string descricao, string unidade, string nome) ParseNumericField(string text)
    {
        var unitMatch = Regex.Match(text, @"\b(mm|cm|kg|m²|m2|ml|bpm|lpm|%|milímetros|milimetros|centímetros|centimetros|quilos|quilogramas)\b", RegexOptions.IgnoreCase);
        var unidade = unitMatch.Success ? NormalizeUnit(unitMatch.Groups[1].Value) : InferUnitFromField(text);

        var cleaned = Regex.Replace(text, @"\b(adicionar|adiciona|incluir|inclua|campo|medida|medidas|de|da|do|em|para|com|normalidade|homem|mulher|masculino|feminino|\d+(?:[.,]\d+)?|a|até|-)\b", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "Nova medida";

        var descricao = char.ToUpper(cleaned[0]) + cleaned[1..];
        return (descricao, unidade, ToVarName(descricao));
    }

    private static string ExtractFieldName(string text)
    {
        var m = Regex.Match(text, @"(?:remover|remove|excluir|exclua|apagar|apague)\s+(?:o\s+)?(?:campo\s+)?(.+)$", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim().TrimEnd('.') : text;
    }

    private static string ExtractSectionName(string text)
    {
        var m = Regex.Match(text, @"(?:criar|crie|nova|adicionar)\s+(?:a\s+)?(?:seção|secao|sessão|sessao)\s+(.+)$", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim().TrimEnd('.') : "NOVA SEÇÃO";
    }

    private static bool ContainsEditAddIntent(string text) =>
        text.Contains("adicion", StringComparison.OrdinalIgnoreCase)
        || text.Contains("inclu", StringComparison.OrdinalIgnoreCase)
        || text.Contains("criar campo", StringComparison.OrdinalIgnoreCase);

    private static string InferUnitFromField(string text)
    {
        if (text.Contains("altura", StringComparison.OrdinalIgnoreCase)) return "cm";
        if (text.Contains("peso", StringComparison.OrdinalIgnoreCase)) return "kg";
        if (text.Contains("superfície", StringComparison.OrdinalIgnoreCase) || text.Contains("superficie", StringComparison.OrdinalIgnoreCase)) return "m²";
        return "mm";
    }

    private static string NormalizeUnit(string unit) => unit.ToLowerInvariant() switch
    {
        "m2" => "m²",
        "milímetros" or "milimetros" => "mm",
        "centímetros" or "centimetros" => "cm",
        "quilos" or "quilogramas" => "kg",
        _ => unit.Replace("m2", "m²", StringComparison.OrdinalIgnoreCase)
    };

    private static string ToVarName(string value) =>
        Regex.Replace(value.ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');

    private static string ExportToModoTexto(string camposScriptJson)
    {
        var root = ParseRoot(camposScriptJson);
        var lines = new List<string>();
        var campos = root["camposScript"] as JsonArray ?? new JsonArray();

        foreach (var node in campos)
        {
            var field = node?.AsObject();
            if (field is null) continue;
            var tipo = field["tipo"]?.GetValue<string>() ?? "";
            var descricao = field["descricao"]?.GetValue<string>() ?? "";
            var nome = field["nome"]?.GetValue<string>() ?? "";

            if (tipo == "etiqueta")
            {
                lines.Add($"[{descricao.ToUpperInvariant()}]");
                continue;
            }

            if (tipo == "numero")
            {
                var unidade = field["medida"]?.GetValue<string>() ?? "";
                var refs = field["referenciaNormalidade"] as JsonArray;
                var mRef = " a ";
                var fRef = " a ";
                if (refs is not null)
                {
                    foreach (var r in refs)
                    {
                        var sexo = r?["sexo"]?.GetValue<string>();
                        var min = ModoTextoCodigoFormatter.FormatDecimalText(r?["valorMin"]?.GetValue<string>());
                        var max = ModoTextoCodigoFormatter.FormatDecimalText(r?["valorMax"]?.GetValue<string>());
                        if (sexo == "M") mRef = $"{min} a {max}";
                        if (sexo == "F") fRef = $"{min} a {max}";
                    }
                }

                lines.Add($"{descricao} ({nome}): 0.0  {unidade} (M: {mRef}) (F: {fRef})");
            }
        }

        return string.Join("\n", lines) + "\n";
    }

    private static string ExportToHtml(string camposScriptJson)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<style>");
        sb.AppendLine("    .campoMedida { border-radius: 5px; border: 1px solid #e3e3e3; width: 63px; height: 30px; margin: 5px 2px 0 0; padding-right: 6px; font-size: 16px; text-align: end; }");
        sb.AppendLine("    .unidadeMedida { font-size: 14px; width: 45px; text-align: left; color: #333; }");
        sb.AppendLine("    .referencia { font-size: 14px; text-align: left; color: #484848; }");
        sb.AppendLine("    .title { font-weight: bold; background: #e6e6e6; padding: 5px; border-radius: 5px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("<div id=\"containerHtml\"><div class=\"d-flex laudo-layout\"><div id=\"CollapseColunaEsquerda\" class=\"laudo-coluna\" ats=\"imprimir\" percent=\"50\" indexImpressao=\"1\">");

        var root = ParseRoot(camposScriptJson);
        var campos = root["camposScript"] as JsonArray ?? new JsonArray();
        foreach (var node in campos)
        {
            var field = node?.AsObject();
            if (field is null) continue;
            var tipo = field["tipo"]?.GetValue<string>() ?? "";
            var descricao = field["descricao"]?.GetValue<string>() ?? "";
            var nome = field["nome"]?.GetValue<string>() ?? "";

            if (tipo == "etiqueta")
            {
                sb.AppendLine($"<div class=\"title\">{descricao.ToUpperInvariant()}</div>");
                continue;
            }

            if (tipo == "numero")
            {
                var unidade = field["medida"]?.GetValue<string>() ?? "";
                var id = nome.StartsWith("VR_", StringComparison.OrdinalIgnoreCase) ? nome : $"VR_{nome}";
                var reference = BuildReference(field);
                sb.AppendLine($"""
            <div class="laudo-linha">
                <div class="descricaoMedida">
                    <span for="{id}">{descricao}</span>
                </div>
                <div class="laudo-campo-wrapper">
                    <input id="{id}" class="campoMedida" type="number" step="any">
                    <span class="unidadeMedida">{unidade}</span>
                    <span for="{id}" class="referencia ml-2" id="{id}_NORMALIDADE">{reference}</span>
                    <span for="{id}" id="{id}_COMENTARIONORMALIDADE"></span>
                </div>
            </div>
""");
            }
        }

        sb.AppendLine("</div></div></div>");
        sb.AppendLine("<script>function iniciarFuncoes(){} iniciarFuncoes();</script>");
        return sb.ToString();
    }

    private static string BuildReference(JsonObject field)
    {
        var refs = field["referenciaNormalidade"] as JsonArray;
        if (refs is null || refs.Count == 0) return "M:  a  F:  ";

        var mRef = "  a  ";
        var fRef = "  a  ";
        foreach (var r in refs)
        {
            var sexo = r?["sexo"]?.GetValue<string>();
            var min = r?["valorMin"]?.GetValue<string>() ?? "";
            var max = r?["valorMax"]?.GetValue<string>() ?? "";
            if (sexo == "M") mRef = $"{min} a {max}";
            if (sexo == "F") fRef = $"{min} a {max}";
        }

        return $"M: {mRef} F: {fRef}";
    }

    [GeneratedRegex(@"\b(remover|remove|excluir|exclua|apagar|apague)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RemoveFieldPattern();

    [GeneratedRegex(@"\b(criar|crie|nova)\s+(seção|secao|sessão|sessao)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AddSectionPattern();

    [GeneratedRegex(
        @"\b(?:altura|peso|superfície corporal|superficie corporal|anel\s+aórtico|anel aortico|via de saída|via de saida|menor óptica|seios de valsalva|diâmetro|diametro|espessura|volume|fração|fracao|ve|vd|ae|ao|imc)[^,.;]*?(?:mm|cm|kg|m²|m2|ml|bpm|lpm|%)",
        RegexOptions.IgnoreCase)]
    private static partial Regex FieldPhrasePattern();
}
