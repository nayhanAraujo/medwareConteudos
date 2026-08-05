namespace ConversorHtml.Application.Interfaces;

public interface IImageToHtmlConverter
{
    Task<string> ConvertAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
}
