using ConversorHtml.Application.Dtos;

namespace ConversorHtml.Application.Interfaces;

public interface IImageMeasureAnalyzer
{
    Task<MeasureExtractionResultDto> AnalyzeAsync(
        Stream imageStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
