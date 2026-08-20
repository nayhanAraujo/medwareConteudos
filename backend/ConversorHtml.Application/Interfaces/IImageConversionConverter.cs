using ConversorHtml.Domain.Enums;

namespace ConversorHtml.Application.Interfaces;

public interface IImageConversionConverter
{
    Task<string> ConvertAsync(
        Stream imageStream,
        string fileName,
        ConversionOutputFormat format,
        CancellationToken cancellationToken = default);
}
