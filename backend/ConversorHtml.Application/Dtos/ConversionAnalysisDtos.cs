namespace ConversorHtml.Application.Dtos;

public sealed class ExtractedMeasureDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string? VariableName { get; set; }
    public string? Section { get; set; }
    public string? Unit { get; set; }
    public string? OriginalText { get; set; }
}

public sealed class MeasureExtractionResultDto
{
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string Provider { get; set; } = string.Empty;
    public IReadOnlyList<ExtractedMeasureDto> Measures { get; set; } = [];
}
