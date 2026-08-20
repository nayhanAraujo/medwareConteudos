using System.Text.RegularExpressions;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Models;

namespace ConversorHtml.Application.Services;

/// <summary>
/// Valida TXT modo texto conforme docs/AGENTE-MODELOS-MODO-TEXTO.md (checklist seção 9).
/// </summary>
public partial class LaudosUxModoTextoValidator : ILaudosUxModoTextoValidator
{
    public ValidationResult Validate(string text)
    {
        var result = new ValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(text))
        {
            result.IsValid = false;
            result.Errors.Add("TXT vazio.");
            return result;
        }

        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("TXT sem linhas de conteúdo.");
            return result;
        }

        var hasSection = false;
        var variableNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (line.Contains("HASH", StringComparison.OrdinalIgnoreCase))
            {
                AddWarning(result, $"Linha com HASH será ignorada na importação: {Truncate(line, 80)}");
                continue;
            }

            if (line.StartsWith('['))
            {
                hasSection = true;
                continue;
            }

            if (line.StartsWith('*'))
            {
                TrackVariable(line, variableNames, result);
                continue;
            }

            if (line.Contains("[Lista]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[Única]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[UNICA]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[Múltipla]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("[MULTIPLA]", StringComparison.OrdinalIgnoreCase))
            {
                if (LooksLikeConcatenatedListLine(line))
                {
                    AddError(result, "Possível concatenação de dois campos na mesma linha após marcador de lista.");
                }

                TrackVariable(line, variableNames, result);
                continue;
            }

            if (line.Contains(':'))
            {
                ValidateNumericLine(line, result);
                TrackVariable(line, variableNames, result);
                continue;
            }

            AddWarning(result, $"Linha não reconhecida pelo parser TXT: {Truncate(line, 80)}");
        }

        if (!hasSection)
        {
            AddWarning(result, "Nenhuma etiqueta de seção [SEÇÃO] encontrada.");
        }

        return result;
    }

    private static void ValidateNumericLine(string line, ValidationResult result)
    {
        if (!VariablePattern().IsMatch(line))
        {
            AddWarning(result, $"Campo numérico sem variável entre parênteses: {Truncate(line, 80)}");
        }

        if (line.Contains("Código:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Codigo:", StringComparison.OrdinalIgnoreCase))
        {
            var codeIdx = line.IndexOf("Código:", StringComparison.OrdinalIgnoreCase);
            if (codeIdx < 0)
            {
                codeIdx = line.IndexOf("Codigo:", StringComparison.OrdinalIgnoreCase);
            }

            var codePart = line[codeIdx..];
            if (!codePart.Contains("<<VR_", StringComparison.Ordinal))
            {
                AddError(result, "Código: deve referenciar variáveis com <<VR_...>>.");
            }
        }

        if (line.Contains("(M:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("(F:", StringComparison.OrdinalIgnoreCase))
        {
            if (!ReferencePattern().IsMatch(line))
            {
                AddWarning(result, $"Referência (M:) ou (F:) possivelmente malformada: {Truncate(line, 80)}");
            }
        }
    }

    private static void TrackVariable(string line, Dictionary<string, int> variableNames, ValidationResult result)
    {
        var match = VariablePattern().Match(line);
        if (!match.Success)
        {
            return;
        }

        var name = match.Groups[1].Value;
        if (name.StartsWith("VR_", StringComparison.OrdinalIgnoreCase))
        {
            AddWarning(result, $"Variável ({name}) não deve usar prefixo VR_ no TXT.");
        }

        variableNames.TryGetValue(name, out var count);
        variableNames[name] = count + 1;
        if (count >= 1)
        {
            AddError(result, $"Variável duplicada: ({name}).");
        }
    }

    private static bool LooksLikeConcatenatedListLine(string line)
    {
        var markerIdx = line.LastIndexOf("[Lista]", StringComparison.OrdinalIgnoreCase);
        if (markerIdx < 0)
        {
            markerIdx = line.LastIndexOf("[UNICA]", StringComparison.OrdinalIgnoreCase);
        }
        if (markerIdx < 0)
        {
            markerIdx = line.LastIndexOf("[MULTIPLA]", StringComparison.OrdinalIgnoreCase);
        }
        if (markerIdx < 0)
        {
            return false;
        }

        var after = line[(markerIdx + 7)..].Trim();
        return after.Contains('=') || after.Contains('(');
    }

    private static void AddError(ValidationResult result, string message)
    {
        result.IsValid = false;
        result.Errors.Add(message);
    }

    private static void AddWarning(ValidationResult result, string message) =>
        result.Warnings.Add(message);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";

    [GeneratedRegex(@"\(([A-Za-z0-9_]+)\)", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();

    [GeneratedRegex(@"\([MF]:\s*[^)]+\)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();
}
