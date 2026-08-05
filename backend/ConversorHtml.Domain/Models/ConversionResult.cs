namespace ConversorHtml.Domain.Models;

public class ConversionResult
{
    public string Html { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime ConvertedAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public ValidationResult Validation { get; set; } = new();
}
