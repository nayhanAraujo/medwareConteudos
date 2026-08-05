using System.Text.RegularExpressions;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Models;

namespace ConversorHtml.Application.Services;

/// <summary>
/// Valida HTML LaudosUX conforme manual_scripts_html_UX.md.
/// Erros = regras que quebram o sistema; Warnings = recomendações / sinais fracos.
/// </summary>
public partial class LaudosUxHtmlValidator : ILaudosUxHtmlValidator
{
    private static readonly string[] ForbiddenTags =
    [
        "<!DOCTYPE", "<html", "<head", "<body", "<meta", "<title", "<link"
    ];

    public ValidationResult Validate(string html)
    {
        var result = new ValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(html))
        {
            result.IsValid = false;
            result.Errors.Add("HTML vazio.");
            return result;
        }

        var normalized = html.Trim();

        ValidateForbiddenTags(normalized, result);
        ValidateStructureOrder(normalized, result);
        ValidateContainerHtml(normalized, result);
        ValidateNoLayoutTables(normalized, result);
        ValidateNoInlineCss(normalized, result);
        ValidateScriptInitialization(normalized, result);
        ValidatePersistenceIds(normalized, result);
        ValidatePrintSystem(normalized, result);
        ValidateFieldPatterns(normalized, result);

        return result;
    }

    private static void ValidateForbiddenTags(string html, ValidationResult result)
    {
        foreach (var tag in ForbiddenTags)
        {
            if (html.Contains(tag, StringComparison.OrdinalIgnoreCase))
            {
                AddError(result, $"Tag proibida detectada: {tag}. O script LaudosUX não pode conter documento HTML completo.");
            }
        }
    }

    private static void ValidateStructureOrder(string html, ValidationResult result)
    {
        var styleIdx = html.IndexOf("<style", StringComparison.OrdinalIgnoreCase);
        var containerIdx = html.IndexOf("id=\"containerHtml\"", StringComparison.OrdinalIgnoreCase);
        if (containerIdx < 0)
        {
            containerIdx = html.IndexOf("id='containerHtml'", StringComparison.OrdinalIgnoreCase);
        }

        var scriptIdx = html.IndexOf("<script", StringComparison.OrdinalIgnoreCase);

        if (styleIdx < 0)
        {
            AddError(result, "Obrigatório conter a tag <style>.");
        }
        else if (!html.TrimStart().StartsWith("<style", StringComparison.OrdinalIgnoreCase))
        {
            AddError(result, "O HTML deve iniciar com a tag <style>.");
        }

        if (containerIdx < 0)
        {
            AddError(result, "Obrigatório conter <div id=\"containerHtml\">.");
        }

        if (scriptIdx < 0)
        {
            AddError(result, "Obrigatório conter a tag <script>.");
        }

        if (styleIdx >= 0 && containerIdx >= 0 && scriptIdx >= 0)
        {
            if (!(styleIdx < containerIdx && containerIdx < scriptIdx))
            {
                AddError(result, "Ordem obrigatória: <style> → <div id=\"containerHtml\"> → <script>.");
            }
        }
    }

    private static void ValidateContainerHtml(string html, ValidationResult result)
    {
        var matches = ContainerHtmlOpenTagRegex().Matches(html);
        if (matches.Count == 0)
        {
            return;
        }

        if (matches.Count > 1)
        {
            AddError(result, "Deve existir apenas um elemento com id=\"containerHtml\".");
        }

        foreach (Match match in matches)
        {
            var openTag = match.Value;
            // Manual: NÃO PODENDO haver nenhum outro atributo (classe ou id) além de id="containerHtml"
            var hasClass = ClassAttrRegex().IsMatch(openTag);
            var otherIds = IdAttrRegex().Matches(openTag)
                .Cast<Match>()
                .Any(m => !m.Groups[1].Value.Equals("containerHtml", StringComparison.OrdinalIgnoreCase));

            if (hasClass || otherIds)
            {
                AddError(result,
                    "A div raiz deve ser apenas <div id=\"containerHtml\">, sem class ou outros atributos de id.");
            }
        }
    }

    private static void ValidateNoLayoutTables(string html, ValidationResult result)
    {
        if (TableTagRegex().IsMatch(html))
        {
            AddError(result, "Uso de <table> para layout não é permitido. Use flexbox/grid com <div>, <section> ou <span>.");
        }
    }

    private static void ValidateNoInlineCss(string html, ValidationResult result)
    {
        // Ignora style= dentro da tag <style>...</style>
        var withoutStyleBlocks = StyleBlockRegex().Replace(html, string.Empty);
        if (InlineStyleRegex().IsMatch(withoutStyleBlocks))
        {
            AddError(result, "CSS inline (style=\"...\") não é permitido.");
        }
    }

    private static void ValidateScriptInitialization(string html, ValidationResult result)
    {
        var scriptMatch = ScriptBlockRegex().Match(html);
        if (!scriptMatch.Success)
        {
            return;
        }

        var script = scriptMatch.Groups[1].Value;

        if (!IniciarFuncoesDeclRegex().IsMatch(script))
        {
            AddError(result, "Obrigatório declarar function iniciarFuncoes() no <script>.");
        }

        var calls = IniciarFuncoesCallRegex().Matches(script);
        if (calls.Count == 0)
        {
            AddError(result, "Obrigatório chamar iniciarFuncoes() no final do script.");
        }
        else
        {
            var lastCall = calls[^1];
            var afterCall = script[(lastCall.Index + lastCall.Length)..].Trim();
            // Após a chamada final, só comentários ou espaços
            var cleaned = LineCommentRegex().Replace(afterCall, string.Empty).Trim();
            cleaned = BlockCommentRegex().Replace(cleaned, string.Empty).Trim();
            if (!string.IsNullOrEmpty(cleaned))
            {
                AddError(result, "A chamada iniciarFuncoes() deve ser a última instrução do <script>.");
            }
        }

        if (DomContentLoadedRegex().IsMatch(script))
        {
            AddError(result, "Não use document.addEventListener('DOMContentLoaded') — não funciona no LaudosUX. Use iniciarFuncoes().");
        }

        if (WindowOnloadRegex().IsMatch(script))
        {
            AddError(result, "Não use window.onload — não funciona no LaudosUX. Use iniciarFuncoes().");
        }

        if (SetTimeoutRegex().IsMatch(script))
        {
            AddError(result, "Não use setTimeout para inicialização — não funciona no LaudosUX. Use iniciarFuncoes().");
        }
    }

    private static void ValidatePersistenceIds(string html, ValidationResult result)
    {
        var inputs = InputTagRegex().Matches(html);
        if (inputs.Count == 0)
        {
            result.Warnings.Add("Nenhum <input> encontrado. Campos persistíveis devem usar id iniciando com VR_.");
            return;
        }

        var vrCount = 0;
        var invalidIds = new List<string>();
        var missingIds = 0;

        foreach (Match input in inputs)
        {
            var tag = input.Value;
            var idMatch = IdAttrRegex().Match(tag);
            var hasCampoMedida = ClassContains(tag, "campoMedida");
            var typeMatch = TypeAttrRegex().Match(tag);
            var isNumber = typeMatch.Success
                && typeMatch.Groups[1].Value.Equals("number", StringComparison.OrdinalIgnoreCase);
            var isMeasureLike = hasCampoMedida || isNumber;

            if (!idMatch.Success)
            {
                if (isMeasureLike)
                {
                    missingIds++;
                }

                continue;
            }

            var id = idMatch.Groups[1].Value;
            if (id.StartsWith("VR_", StringComparison.OrdinalIgnoreCase))
            {
                vrCount++;
                continue;
            }

            if (isMeasureLike)
            {
                invalidIds.Add(id);
            }
        }

        if (missingIds > 0)
        {
            AddError(result,
                $"Campos de medida sem id. Obrigatório id iniciando com VR_ (ex.: VR_PESO). Encontrados: {missingIds}.");
        }

        if (invalidIds.Count > 0)
        {
            var sample = string.Join(", ", invalidIds.Distinct(StringComparer.OrdinalIgnoreCase).Take(8));
            AddError(result,
                $"IDs de campos devem iniciar com VR_ para persistência. Inválidos: {sample}.");
        }

        if (vrCount == 0 && inputs.Count > 0)
        {
            AddError(result,
                "Nenhum input com id VR_* encontrado. Campos que precisam ser salvos devem usar a convenção VR_.");
        }
    }

    private static void ValidatePrintSystem(string html, ValidationResult result)
    {
        if (!ClassContainsAnywhere(html, "d-flex"))
        {
            AddError(result,
                "Obrigatório envolver o conteúdo em <div class=\"d-flex\"> para adaptação correta da impressão.");
        }

        var printBlocks = AtsImprimirBlockRegex().Matches(html);
        if (printBlocks.Count == 0)
        {
            AddError(result,
                "Obrigatório conter ao menos um bloco com ats=\"imprimir\" para conteúdo impresso.");
            return;
        }

        var collapsePrintCount = 0;
        foreach (Match block in printBlocks)
        {
            var tag = block.Value;

            if (!PercentAttrRegex().IsMatch(tag))
            {
                AddError(result, "Bloco ats=\"imprimir\" deve conter o atributo percent (ex.: percent=\"50\").");
            }

            if (!IndexImpressaoAttrRegex().IsMatch(tag))
            {
                AddError(result, "Bloco ats=\"imprimir\" deve conter o atributo indexImpressao (ex.: indexImpressao=\"1\").");
            }

            var idMatch = IdAttrRegex().Match(tag);
            if (!idMatch.Success)
            {
                AddError(result, "Bloco ats=\"imprimir\" deve ter id iniciando com Collapse (ex.: id=\"CollapseDadosPaciente\").");
            }
            else if (!idMatch.Groups[1].Value.StartsWith("Collapse", StringComparison.Ordinal))
            {
                AddError(result,
                    $"Id do bloco de impressão deve iniciar com Collapse. Encontrado: id=\"{idMatch.Groups[1].Value}\".");
            }
            else
            {
                collapsePrintCount++;
            }
        }

        // Duas colunas / blocos de impressão
        if (collapsePrintCount < 2)
        {
            result.Warnings.Add(
                "Layout em duas colunas: recomenda-se ao menos dois blocos Collapse com ats=\"imprimir\" (uma coluna cada).");
        }

        // Inputs de medida precisam de campoMedida para impressão condicional
        var measureInputs = InputTagRegex().Matches(html)
            .Cast<Match>()
            .Where(m =>
            {
                var tag = m.Value;
                var id = IdAttrRegex().Match(tag);
                return id.Success && id.Groups[1].Value.StartsWith("VR_", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var missingCampoMedida = measureInputs
            .Where(m => !ClassContains(m.Value, "campoMedida"))
            .Select(m => IdAttrRegex().Match(m.Value).Groups[1].Value)
            .Distinct()
            .ToList();

        if (missingCampoMedida.Count > 0)
        {
            var sample = string.Join(", ", missingCampoMedida.Take(8));
            AddError(result,
                $"Inputs VR_* devem ter class=\"campoMedida\" para impressão somente se preenchidos. Sem classe: {sample}.");
        }
    }

    private static void ValidateFieldPatterns(string html, ValidationResult result)
    {
        var vrIds = InputTagRegex().Matches(html)
            .Cast<Match>()
            .Select(m => IdAttrRegex().Match(m.Value))
            .Where(m => m.Success && m.Groups[1].Value.StartsWith("VR_", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id =>
                !id.EndsWith("_NORMALIDADE", StringComparison.OrdinalIgnoreCase)
                && !id.EndsWith("_COMENTARIONORMALIDADE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (vrIds.Count == 0)
        {
            return;
        }

        var missingDescricao = new List<string>();
        var missingFor = new List<string>();

        foreach (var id in vrIds)
        {
            var hasDescricao = DescricaoMedidaForRegex(id).IsMatch(html);
            if (!hasDescricao)
            {
                if (!SpanForRegex(id).IsMatch(html))
                {
                    missingFor.Add(id);
                }
                else if (!html.Contains("descricaoMedida", StringComparison.OrdinalIgnoreCase))
                {
                    missingDescricao.Add(id);
                }
                else if (!DescricaoMedidaBlockWithForRegex(id).IsMatch(html))
                {
                    missingDescricao.Add(id);
                }
            }

            ValidateNormalidadeForMeasure(html, id, result);
            ValidateComentarioNormalidadeForMeasure(html, id, result);
        }

        if (missingFor.Count > 0)
        {
            var sample = string.Join(", ", missingFor.Take(8));
            AddError(result,
                $"Padrões de Desenvolvimento: cada medida precisa de <span for=\"ID\"> com o mesmo id do input. Faltando for= para: {sample}.");
        }

        if (missingDescricao.Count > 0)
        {
            var sample = string.Join(", ", missingDescricao.Take(8));
            AddError(result,
                $"Padrões de Desenvolvimento: o nome da medida deve estar em <div class=\"descricaoMedida\">. Verificar campos: {sample}.");
        }

        ValidateOrphanNormalidadeIds(html, vrIds, result);
        ValidateOrphanComentarioIds(html, vrIds, result);
    }

    /// <summary>
    /// Cada input VR_* deve ter &lt;span for="VR_*" id="VR_*_NORMALIDADE"&gt;.
    /// </summary>
    private static void ValidateNormalidadeForMeasure(string html, string measureId, ValidationResult result)
    {
        var expectedId = $"{measureId}_NORMALIDADE";
        var tag = FindTagContainingId(html, expectedId);

        if (tag is null)
        {
            // Procura ids "parecidos" incorretos ligados ao campo (class referencia + for, ou id parcial)
            var wrongId = FindWrongNormalidadeId(html, measureId);
            if (wrongId is not null)
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: a normalidade de {measureId} deve ter id=\"{expectedId}\" " +
                    $"(nome da medida + sufixo _NORMALIDADE). Encontrado id=\"{wrongId}\".");
            }
            else
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: falta o campo de normalidade de {measureId}. " +
                    $"Obrigatório: <span for=\"{measureId}\" id=\"{expectedId}\"> (id = nome da medida + _NORMALIDADE).");
            }

            return;
        }

        if (!tag.Contains("span", StringComparison.OrdinalIgnoreCase))
        {
            AddError(result,
                $"Padrões de Desenvolvimento: a normalidade {expectedId} deve ser um <span> com for e id.");
        }

        var forMatch = ForAttrRegex().Match(tag);
        if (!forMatch.Success)
        {
            AddError(result,
                $"Padrões de Desenvolvimento: a normalidade {expectedId} deve ter for=\"{measureId}\".");
        }
        else if (!forMatch.Groups[1].Value.Equals(measureId, StringComparison.OrdinalIgnoreCase))
        {
            AddError(result,
                $"Padrões de Desenvolvimento: a normalidade {expectedId} está com for=\"{forMatch.Groups[1].Value}\", " +
                $"mas deve ser for=\"{measureId}\" (mesmo id do input da medida).");
        }
    }

    /// <summary>
    /// Cada input VR_* deve ter &lt;span for="VR_*" id="VR_*_COMENTARIONORMALIDADE"&gt;.
    /// </summary>
    private static void ValidateComentarioNormalidadeForMeasure(string html, string measureId, ValidationResult result)
    {
        var expectedId = $"{measureId}_COMENTARIONORMALIDADE";
        var tag = FindTagContainingId(html, expectedId);

        if (tag is null)
        {
            var wrongId = FindWrongComentarioId(html, measureId);
            if (wrongId is not null)
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: o comentário da normalidade de {measureId} deve ter id=\"{expectedId}\" " +
                    $"(nome da medida + sufixo _COMENTARIONORMALIDADE). Encontrado id=\"{wrongId}\".");
            }
            else
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: falta o comentário da normalidade de {measureId}. " +
                    $"Obrigatório: <span for=\"{measureId}\" id=\"{expectedId}\"> (id = nome da medida + _COMENTARIONORMALIDADE).");
            }

            return;
        }

        if (!tag.Contains("span", StringComparison.OrdinalIgnoreCase))
        {
            AddError(result,
                $"Padrões de Desenvolvimento: o comentário {expectedId} deve ser um <span> com for e id.");
        }

        var forMatch = ForAttrRegex().Match(tag);
        if (!forMatch.Success)
        {
            AddError(result,
                $"Padrões de Desenvolvimento: o comentário {expectedId} deve ter for=\"{measureId}\".");
        }
        else if (!forMatch.Groups[1].Value.Equals(measureId, StringComparison.OrdinalIgnoreCase))
        {
            AddError(result,
                $"Padrões de Desenvolvimento: o comentário {expectedId} está com for=\"{forMatch.Groups[1].Value}\", " +
                $"mas deve ser for=\"{measureId}\" (mesmo id do input da medida).");
        }
    }

    private static string? FindWrongNormalidadeId(string html, string measureId)
    {
        // class="referencia" com for=measureId mas id errado
        foreach (Match m in SpanWithForRegex(measureId).Matches(html))
        {
            var tag = m.Value;
            if (!ClassContains(tag, "referencia"))
            {
                continue;
            }

            var idMatch = IdAttrRegex().Match(tag);
            if (!idMatch.Success)
            {
                return "(sem id)";
            }

            var id = idMatch.Groups[1].Value;
            if (!id.Equals($"{measureId}_NORMALIDADE", StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        // id que contém NORMALIDADE e o nome da medida, mas não no formato exato
        foreach (Match m in IdAttrRegex().Matches(html))
        {
            var id = m.Groups[1].Value;
            if (id.EndsWith("_COMENTARIONORMALIDADE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (id.Contains("NORMALIDADE", StringComparison.OrdinalIgnoreCase)
                && id.Contains(measureId, StringComparison.OrdinalIgnoreCase)
                && !id.Equals($"{measureId}_NORMALIDADE", StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    private static string? FindWrongComentarioId(string html, string measureId)
    {
        foreach (Match m in IdAttrRegex().Matches(html))
        {
            var id = m.Groups[1].Value;
            if (id.Contains("COMENTARIO", StringComparison.OrdinalIgnoreCase)
                && id.Contains(measureId, StringComparison.OrdinalIgnoreCase)
                && !id.Equals($"{measureId}_COMENTARIONORMALIDADE", StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    private static void ValidateOrphanNormalidadeIds(string html, List<string> vrIds, ValidationResult result)
    {
        foreach (Match m in NormalidadeIdRegex().Matches(html))
        {
            var fullId = m.Groups[1].Value;
            if (fullId.EndsWith("_COMENTARIONORMALIDADE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!fullId.EndsWith("_NORMALIDADE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var baseId = fullId[..^"_NORMALIDADE".Length];
            if (!baseId.StartsWith("VR_", StringComparison.OrdinalIgnoreCase))
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: id de normalidade \"{fullId}\" deve iniciar com o nome da medida (VR_...) e terminar com _NORMALIDADE.");
                continue;
            }

            if (!vrIds.Contains(baseId, StringComparer.OrdinalIgnoreCase))
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: normalidade \"{fullId}\" não tem <input id=\"{baseId}\"> correspondente.");
            }
        }
    }

    private static void ValidateOrphanComentarioIds(string html, List<string> vrIds, ValidationResult result)
    {
        foreach (Match m in ComentarioNormalidadeIdRegex().Matches(html))
        {
            var fullId = m.Groups[1].Value;
            var baseId = fullId[..^"_COMENTARIONORMALIDADE".Length];

            if (!baseId.StartsWith("VR_", StringComparison.OrdinalIgnoreCase))
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: id de comentário \"{fullId}\" deve iniciar com o nome da medida (VR_...) e terminar com _COMENTARIONORMALIDADE.");
                continue;
            }

            if (!vrIds.Contains(baseId, StringComparer.OrdinalIgnoreCase))
            {
                AddError(result,
                    $"Padrões de Desenvolvimento: comentário \"{fullId}\" não tem <input id=\"{baseId}\"> correspondente.");
            }
        }
    }

    private static Regex SpanWithForRegex(string measureId) =>
        new(
            $@"<\s*span\b[^>]*\bfor\s*=\s*[""']{Regex.Escape(measureId)}[""'][^>]*>",
            RegexOptions.IgnoreCase);

    private static bool ClassContains(string tag, string className) =>
        Regex.IsMatch(
            tag,
            $@"class\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']",
            RegexOptions.IgnoreCase);

    private static bool ClassContainsAnywhere(string html, string className) =>
        Regex.IsMatch(
            html,
            $@"class\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']",
            RegexOptions.IgnoreCase);

    private static string? FindTagContainingId(string html, string id)
    {
        var match = Regex.Match(
            html,
            $@"<([a-zA-Z0-9]+)([^>]*\bid\s*=\s*[""']{Regex.Escape(id)}[""'][^>]*)>",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    private static void AddError(ValidationResult result, string message)
    {
        result.IsValid = false;
        if (!result.Errors.Contains(message))
        {
            result.Errors.Add(message);
        }
    }

    private static Regex DescricaoMedidaForRegex(string id) =>
        new(
            $@"class\s*=\s*[""'][^""']*\bdescricaoMedida\b[^""']*[""'][^>]*>[\s\S]*?for\s*=\s*[""']{Regex.Escape(id)}[""']",
            RegexOptions.IgnoreCase);

    private static Regex DescricaoMedidaBlockWithForRegex(string id) =>
        new(
            $@"<div[^>]*class\s*=\s*[""'][^""']*\bdescricaoMedida\b[^""']*[""'][^>]*>[\s\S]*?for\s*=\s*[""']{Regex.Escape(id)}[""'][\s\S]*?</div>",
            RegexOptions.IgnoreCase);

    private static Regex SpanForRegex(string id) =>
        new($@"<\s*span[^>]*\bfor\s*=\s*[""']{Regex.Escape(id)}[""']", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"\sstyle\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyleRegex();

    [GeneratedRegex(@"<style\b[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleBlockRegex();

    [GeneratedRegex(@"<script\b[^>]*>([\s\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlockRegex();

    [GeneratedRegex(@"<\s*table\b", RegexOptions.IgnoreCase)]
    private static partial Regex TableTagRegex();

    [GeneratedRegex(@"<\s*div\b[^>]*\bid\s*=\s*[""']containerHtml[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ContainerHtmlOpenTagRegex();

    [GeneratedRegex(@"\bclass\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttrRegex();

    [GeneratedRegex(@"\bid\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex IdAttrRegex();

    [GeneratedRegex(@"\btype\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex TypeAttrRegex();

    [GeneratedRegex(@"\bfor\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ForAttrRegex();

    [GeneratedRegex(@"<\s*input\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex(@"<\s*div\b[^>]*\bats\s*=\s*[""']imprimir[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AtsImprimirBlockRegex();

    [GeneratedRegex(@"\bpercent\s*=\s*[""'][^""']+[""']", RegexOptions.IgnoreCase)]
    private static partial Regex PercentAttrRegex();

    [GeneratedRegex(@"\bindexImpressao\s*=\s*[""'][^""']+[""']", RegexOptions.IgnoreCase)]
    private static partial Regex IndexImpressaoAttrRegex();

    [GeneratedRegex(@"function\s+iniciarFuncoes\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex IniciarFuncoesDeclRegex();

    [GeneratedRegex(@"iniciarFuncoes\s*\(\s*\)\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex IniciarFuncoesCallRegex();

    [GeneratedRegex(@"document\.addEventListener\s*\(\s*['""]DOMContentLoaded['""]", RegexOptions.IgnoreCase)]
    private static partial Regex DomContentLoadedRegex();

    [GeneratedRegex(@"window\.onload\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex WindowOnloadRegex();

    [GeneratedRegex(@"\bsetTimeout\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex SetTimeoutRegex();

    [GeneratedRegex(@"//.*?$", RegexOptions.Multiline)]
    private static partial Regex LineCommentRegex();

    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"\bid\s*=\s*[""']([^""']+_NORMALIDADE)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex NormalidadeIdRegex();

    [GeneratedRegex(@"\bid\s*=\s*[""']([^""']+_COMENTARIONORMALIDADE)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ComentarioNormalidadeIdRegex();
}
