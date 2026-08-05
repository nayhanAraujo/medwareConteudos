namespace ConversorHtml.Application.Dtos;

public class ConversionResponseDto
{
    public string Html { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime ConvertedAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public ValidationResponseDto Validation { get; set; } = new();
}
