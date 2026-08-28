using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MdwConteudos.Api.Modules.Web;

public static class CSharpVariaveisParser
{
    private static readonly Regex VariablePattern = new(@"\bVR_[A-Za-z0-9_]+\b", RegexOptions.Compiled);

    public static ImportacaoVariaveisPreview Parse(string source, string fileName)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException("O arquivo C# está vazio.");
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var root = tree.GetRoot();
        var warnings = tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error)
            .Take(20).Select(x => $"C# linha {x.Location.GetLineSpan().StartLinePosition.Line + 1}: {x.GetMessage()}").ToList();
        var codes = root.DescendantTokens().SelectMany(token => VariablePattern.Matches(token.Text).Select(match => match.Value))
            .Where(code => !code.EndsWith("_CHART", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in VariablePattern.Matches(source)) if (!match.Value.EndsWith("_CHART", StringComparison.OrdinalIgnoreCase)) codes.Add(match.Value);

        var formulas = new List<ImportacaoFormulaItem>();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => x.Identifier.Text.StartsWith("Calcular", StringComparison.OrdinalIgnoreCase)))
        {
            var variable = $"VR_{method.Identifier.Text[8..].ToUpperInvariant()}";
            var expression = method.DescendantNodes().OfType<ReturnStatementSyntax>().FirstOrDefault()?.Expression?.ToString();
            if (!codes.Contains(variable) || string.IsNullOrWhiteSpace(expression)) { warnings.Add($"Método {method.Identifier.Text} não pôde ser associado a uma variável/fórmula."); continue; }
            formulas.Add(new(variable, expression, 1, true, []));
        }

        var normalidades = new List<ImportacaoNormalidadeItem>();
        foreach (var section in root.DescendantNodes().OfType<SwitchSectionSyntax>())
        {
            var chart = section.Labels.SelectMany(x => VariablePattern.Matches(x.ToString()).Select(m => m.Value)).FirstOrDefault(x => x.EndsWith("_CHART", StringComparison.OrdinalIgnoreCase));
            if (chart is null) continue;
            var variable = chart[..^6]; if (!codes.Contains(variable)) continue;
            var assignments = section.DescendantNodes().OfType<AssignmentExpressionSyntax>().GroupBy(x => x.Left.ToString().Split('.').Last(), StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last().Right.ToString(), StringComparer.OrdinalIgnoreCase);
            var min = Decimal(assignments, "Normal") ?? Decimal(assignments, "Min"); var max = Decimal(assignments, "Max");
            if (min.HasValue && max.HasValue) normalidades.Add(new(variable, "M", min, max, 0, 150, null, "Unknown", false, false, ["Referência não localizada."]));
            else warnings.Add($"Faixa de normalidade incompleta para {variable}.");
        }

        var variables = codes.OrderBy(x => x).Select(code => { var shortName = code[3..]; return new ImportacaoVariavelItem(code, shortName.Replace('_', ' '), shortName, shortName, "unknown", false, true, [], formulas.Count(x => x.Variavel.Equals(code, StringComparison.OrdinalIgnoreCase)), normalidades.Count(x => x.Variavel.Equals(code, StringComparison.OrdinalIgnoreCase))); }).ToList();
        return new("cs", fileName, variables, formulas, normalidades, warnings, []);
    }

    public static ImportacaoCsPreview Parse(string source)
    {
        var parsed = Parse(source, "arquivo.cs");
        return new(parsed.Variaveis.Select(x => new ImportacaoCsVariavel(x.Codigo, x.Nome, x.Sigla, x.Abreviacao, x.Unidade, x.ExisteNoBanco)).ToList(), parsed.Formulas.Select(x => new ImportacaoCsFormula(x.Variavel, x.Expressao, x.CasasDecimais)).ToList(), parsed.Normalidades.Where(x => x.ValorMin.HasValue && x.ValorMax.HasValue && x.IdadeMin.HasValue && x.IdadeMax.HasValue).Select(x => new ImportacaoCsNormalidade(x.Variavel, x.Sexo, x.ValorMin!.Value, x.ValorMax!.Value, x.IdadeMin!.Value, x.IdadeMax!.Value, x.Referencia ?? "Unknown")).ToList());
    }

    private static decimal? Decimal(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var raw) && decimal.TryParse(raw.TrimEnd('m', 'M', 'd', 'D', 'f', 'F').Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
}
