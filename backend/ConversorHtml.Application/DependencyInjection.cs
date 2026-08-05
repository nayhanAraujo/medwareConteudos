using ConversorHtml.Application.Configuration;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConversorHtml.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConversionOptions>(configuration.GetSection(ConversionOptions.SectionName));
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));
        services.Configure<CursorOptions>(configuration.GetSection(CursorOptions.SectionName));

        services.AddSingleton<ILaudosUxHtmlValidator, LaudosUxHtmlValidator>();

        var provider = configuration.GetSection(ConversionOptions.SectionName)["Provider"] ?? "Cursor";

        if (!Enum.TryParse<ConversionProvider>(provider, ignoreCase: true, out var conversionProvider))
        {
            conversionProvider = ConversionProvider.Cursor;
        }

        switch (conversionProvider)
        {
            case ConversionProvider.AzureOpenAI:
                services.AddScoped<IImageToHtmlConverter, AzureOpenAiImageToHtmlConverter>();
                break;
            case ConversionProvider.Mock:
                services.AddScoped<IImageToHtmlConverter, MockImageToHtmlConverter>();
                break;
            default:
                services.AddScoped<IImageToHtmlConverter, CursorComposerImageToHtmlConverter>();
                break;
        }

        return services;
    }
}
