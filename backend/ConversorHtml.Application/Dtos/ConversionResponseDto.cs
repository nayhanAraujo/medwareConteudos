using ConversorHtml.Domain.Enums;

namespace ConversorHtml.Application.Dtos;

public class ConversionResponseDto
{
    public ConversionOutputFormat Format { get; set; } = ConversionOutputFormat.Html;
    public string Html { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime ConvertedAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public ValidationResponseDto Validation { get; set; } = new();
}
