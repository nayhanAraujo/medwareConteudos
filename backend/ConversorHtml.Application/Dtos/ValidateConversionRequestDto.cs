namespace ConversorHtml.Application.Dtos;

public class ValidateConversionRequestDto
{
    /// <summary>html | modoTexto (case-insensitive)</summary>
    public string? Format { get; set; }
    public string Content { get; set; } = string.Empty;
}
