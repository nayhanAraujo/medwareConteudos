using System.Globalization;
using System.Text.Json;

namespace MdwConteudos.Api.Modules.Web;

public static class JsonVariaveisParser
{
    public static ImportacaoVariaveisPreview Parse(string source, string fileName)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException("O arquivo JSON está vazio.");
        JsonDocument doc; try { doc = JsonDocument.Parse(source); } catch (JsonException ex) { throw new InvalidOperationException($"JSON inválido: {ex.Message}"); }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("O JSON deve conter um objeto na raiz.");
            if (Property(doc.RootElement, "camposScript") is { ValueKind: JsonValueKind.Array } studioFields) return ParseStudio(studioFields, fileName);
            if (IsNormalidadesMedware(doc.RootElement)) return ParseNormalidadesMedware(doc.RootElement, fileName);
            var formulas = Array(doc.RootElement, "formulas").Select(ParseFormula).ToList(); var normalidades = Array(doc.RootElement, "normalidades").Select(ParseNormalidade).ToList();
            var variables = Array(doc.RootElement, "variaveis").Select(item => { var code = Text(item, "codigo", "sigla", "variavel").Trim(); var errors = new List<string>(); if (string.IsNullOrWhiteSpace(code)) errors.Add("Código/sigla é obrigatório."); var name = Text(item, "nome"); if (string.IsNullOrWhiteSpace(name)) name = code.Replace("VR_", "", StringComparison.OrdinalIgnoreCase).Replace('_', ' '); var sigla = Text(item, "sigla"); if (string.IsNullOrWhiteSpace(sigla)) sigla = code.Replace("VR_", "", StringComparison.OrdinalIgnoreCase); var abbreviation = Text(item, "abreviacao", "abreviação"); if (string.IsNullOrWhiteSpace(abbreviation)) abbreviation = sigla; var unit = Text(item, "unidade"); if (string.IsNullOrWhiteSpace(unit)) unit = "unknown"; return new ImportacaoVariavelItem(code, name, sigla, abbreviation, unit, false, errors.Count == 0, errors, formulas.Count(x => Same(x.Variavel, code, sigla)), normalidades.Count(x => Same(x.Variavel, code, sigla))); }).ToList();
            return new("json", fileName, variables, formulas, normalidades, [], []);
        }
    }

    private static bool IsNormalidadesMedware(JsonElement root) => root.EnumerateObject().Any(property =>
        property.Name.StartsWith("VR_", StringComparison.OrdinalIgnoreCase)
        && property.Value.ValueKind == JsonValueKind.Object
        && (Property(property.Value, "_meta").HasValue
            || new[] { "F", "M", "U", "A" }.Any(sex => Property(property.Value, sex) is { ValueKind: JsonValueKind.Object })));

    private static ImportacaoVariaveisPreview ParseNormalidadesMedware(JsonElement root, string fileName)
    {
        var variables = new List<ImportacaoVariavelItem>();
        var normalidades = new List<ImportacaoNormalidadeItem>();
        var warnings = new List<string>();
        var invalidRanges = 0;

        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.StartsWith("VR_", StringComparison.OrdinalIgnoreCase) || property.Value.ValueKind != JsonValueKind.Object) continue;
            var code = property.Name.Trim();
            var sigla = code[3..];
            var name = sigla.Replace('_', ' ');
            var meta = Property(property.Value, "_meta");
            var reference = meta is { ValueKind: JsonValueKind.Object } metaValue ? Text(metaValue, "Fonte") : "";
            var referenceYear = meta is { ValueKind: JsonValueKind.Object } yearMeta ? NullableInteger(yearMeta, "Ano") : null;
            var page = meta is { ValueKind: JsonValueKind.Object } pageMeta ? NullableInteger(pageMeta, "Pagina") : null;
            var commentNode = Property(property.Value, "COMENTARIOTEXTO");
            var comment = commentNode is { ValueKind: JsonValueKind.Object } commentObject
                ? Text(commentObject, "comentario")
                : commentNode is { ValueKind: JsonValueKind.String } commentText ? commentText.GetString() ?? "" : "";
            var alternatives = Array(property.Value, "VARIAVEISALTERNATIVAS")
                .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var sexName in new[] { "F", "M", "U", "A" })
            {
                if (Property(property.Value, sexName) is not { ValueKind: JsonValueKind.Object } sexBlock) continue;
                foreach (var classification in new[] { "normal", "leve", "moderado", "grave" })
                {
                    if (Property(sexBlock, classification) is not { ValueKind: JsonValueKind.Object } range) continue;
                    var min = NullableDecimal(range, "min");
                    var max = NullableDecimal(range, "max");
                    var errors = new List<string>();
                    if (!min.HasValue || !max.HasValue)
                    {
                        errors.Add("Valores mínimo e máximo são obrigatórios.");
                        invalidRanges++;
                    }
                    if (string.IsNullOrWhiteSpace(reference)) errors.Add("Referência é obrigatória.");
                    normalidades.Add(new(
                        code, sexName.Equals("U", StringComparison.OrdinalIgnoreCase) ? "A" : sexName,
                        min, max, null, null, null, reference, false, false, errors,
                        referenceYear, classification, page, comment));
                }
            }

            variables.Add(new(
                code, name, sigla, sigla, "unknown", false, true, [], 0,
                normalidades.Count(item => item.Variavel.Equals(code, StringComparison.OrdinalIgnoreCase)),
                null, alternatives, []));
        }

        if (invalidRanges > 0) warnings.Add($"{invalidRanges} faixa(s) sem valores mínimo e máximo válidos foram bloqueadas.");
        warnings.Add("Códigos já existentes podem atualizar suas normalidades. Códigos semelhantes podem ser vinculados como alternativas da variável principal.");
        return new("json-normalidades-medware", fileName, variables, [], normalidades, warnings, []);
    }

    private static ImportacaoVariaveisPreview ParseStudio(JsonElement fields, string fileName)
    {
        var variables = new List<ImportacaoVariavelItem>(); var formulas = new List<ImportacaoFormulaItem>(); var normalidades = new List<ImportacaoNormalidadeItem>(); var coloredRanges = 0;
        var allowedTypes = new HashSet<string>(["numero", "combo", "radio", "check", "textarea", "textocurto"], StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields.EnumerateArray())
        {
            var type = Text(field, "tipo"); var name = Text(field, "nome").Trim();
            if (!allowedTypes.Contains(type) || string.IsNullOrWhiteSpace(name)) continue;
            var code = name.StartsWith("VR_", StringComparison.OrdinalIgnoreCase) ? name : $"VR_{name}";
            var description = Text(field, "descricao"); if (string.IsNullOrWhiteSpace(description)) description = name.Replace('_', ' ');
            var unit = Text(field, "medida"); if (string.IsNullOrWhiteSpace(unit)) unit = "unknown";
            var function = Text(field, "funcao"); var decimalPlaces = Integer(field, 1, "casasDecimais");
            if (!string.IsNullOrWhiteSpace(function)) formulas.Add(new(code, function, decimalPlaces, true, []));
            foreach (var range in Array(field, "referenciaNormalidade"))
            {
                var min = NullableDecimal(range, "valorMin"); var max = NullableDecimal(range, "valorMax");
                if (!min.HasValue || !max.HasValue) continue;
                normalidades.Add(new(code, Text(range, "sexo") is { Length: > 0 } sex ? sex : "A", min, max, 0, 150, null, null, false, false, ["Selecione uma referência para as normalidades do JSON Studio."]));
            }
            coloredRanges += Array(field, "normalidades").Count();
            variables.Add(new(code, description, name, name, unit, false, true, [], formulas.Count(x => x.Variavel.Equals(code, StringComparison.OrdinalIgnoreCase)), normalidades.Count(x => x.Variavel.Equals(code, StringComparison.OrdinalIgnoreCase))));
        }
        var warnings = coloredRanges > 0 ? new[] { $"{coloredRanges} faixa(s) colorida(s) do Studio foram preservadas apenas no arquivo de origem e não serão importadas como referência clínica." } : [];
        return new("json-studio", fileName, variables, formulas, normalidades, warnings, []);
    }

    private static ImportacaoFormulaItem ParseFormula(JsonElement item) { var variable = Text(item, "variavel", "codigo", "sigla"); var expression = Text(item, "expressao", "formula"); var errors = new List<string>(); if (string.IsNullOrWhiteSpace(variable)) errors.Add("Variável da fórmula é obrigatória."); if (string.IsNullOrWhiteSpace(expression)) errors.Add("Expressão da fórmula é obrigatória."); return new(variable, expression, Integer(item, 1, "casas_decimais", "casasDecimais"), errors.Count == 0, errors); }
    private static ImportacaoNormalidadeItem ParseNormalidade(JsonElement item) { var variable = Text(item, "variavel", "codigo", "sigla"); var referenceText = Text(item, "referencia"); var referenceCode = NullableInteger(item, "codreferencia", "codReferencia"); var min = NullableDecimal(item, "valormin", "valor_min", "valorMin"); var max = NullableDecimal(item, "valormax", "valor_max", "valorMax"); var errors = new List<string>(); if (string.IsNullOrWhiteSpace(variable)) errors.Add("Variável da normalidade é obrigatória."); if (!min.HasValue || !max.HasValue) errors.Add("Valores mínimo e máximo são obrigatórios."); if (!referenceCode.HasValue && string.IsNullOrWhiteSpace(referenceText)) errors.Add("Referência é obrigatória."); return new(variable, Text(item, "sexo") is { Length: > 0 } sex ? sex : "M", min, max, NullableInteger(item, "idade_min", "idadeMin") ?? 0, NullableInteger(item, "idade_max", "idadeMax") ?? 150, referenceCode, referenceText, false, false, errors); }
    private static IEnumerable<JsonElement> Array(JsonElement root, string name) => Property(root, name) is { ValueKind: JsonValueKind.Array } value ? value.EnumerateArray() : [];
    private static JsonElement? Property(JsonElement element, string name) { foreach (var property in element.EnumerateObject()) if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value; return null; }
    private static string Text(JsonElement element, params string[] names) { foreach (var name in names) if (Property(element, name) is { } value) return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString(); return ""; }
    private static int Integer(JsonElement element, int fallback, params string[] names) => NullableInteger(element, names) ?? fallback;
    private static int? NullableInteger(JsonElement element, params string[] names) { var raw = Text(element, names); return int.TryParse(raw, out var value) ? value : null; }
    private static decimal? NullableDecimal(JsonElement element, params string[] names) { var raw = Text(element, names).Replace(',', '.'); return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null; }
    private static bool Same(string relation, string code, string sigla) => relation.Equals(code, StringComparison.OrdinalIgnoreCase) || relation.Equals(sigla, StringComparison.OrdinalIgnoreCase) || relation.Equals($"VR_{sigla}", StringComparison.OrdinalIgnoreCase);
}
