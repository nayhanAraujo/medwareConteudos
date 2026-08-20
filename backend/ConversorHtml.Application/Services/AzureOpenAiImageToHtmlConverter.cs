using ConversorHtml.Application.Configuration;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ConversorHtml.Application.Services;

/// <summary>
/// Stub para integração futura com Azure OpenAI Vision.
/// Ative via Conversion:Provider = AzureOpenAI em appsettings.json.
/// </summary>
public class AzureOpenAiImageToHtmlConverter : IImageConversionConverter
{
    private readonly AzureOpenAIOptions _options;

    public AzureOpenAiImageToHtmlConverter(IOptions<AzureOpenAIOptions> options)
    {
        _options = options.Value;
    }

    public Task<string> ConvertAsync(
        Stream imageStream,
        string fileName,
        ConversionOutputFormat format,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure OpenAI não configurado. Defina AzureOpenAI:Endpoint e AzureOpenAI:ApiKey em User Secrets.");
        }

        throw new NotImplementedException(
            "Conversão via Azure OpenAI será implementada na fase 2. Use Conversion:Provider = Mock.");
    }
}
