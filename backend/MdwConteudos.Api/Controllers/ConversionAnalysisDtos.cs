namespace MdwConteudos.Api.Controllers;

public sealed class VariableMatchCandidateDto
{
    public int CodVariavel { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Sigla { get; set; } = string.Empty;
    public string? Variavel { get; set; }
    public string? Unidade { get; set; }
    public int Score { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

public sealed class AnalyzedMeasureDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string? VariableName { get; set; }
    public string? Section { get; set; }
    public string? Unit { get; set; }
    public string? OriginalText { get; set; }
    public IReadOnlyList<VariableMatchCandidateDto> Candidates { get; set; } = [];
    public VariableMatchCandidateDto? SelectedCandidate { get; set; }
    public string Status { get; set; } = "unresolved";
}

public sealed class ConversionAnalysisResponseDto
{
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string Provider { get; set; } = string.Empty;
    public IReadOnlyList<AnalyzedMeasureDto> Measures { get; set; } = [];
}

public sealed class ReviewedMeasureDto
{
    public string? Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Section { get; set; }
    public string? Unit { get; set; }
    public string? OriginalText { get; set; }
    public int? CodVariavel { get; set; }
    public string Decision { get; set; } = "keep";
}

public sealed class GenerateModoTextoFromAnalysisRequestDto
{
    public string? SourceFileName { get; set; }
    public IReadOnlyList<ReviewedMeasureDto> Measures { get; set; } = [];
}
