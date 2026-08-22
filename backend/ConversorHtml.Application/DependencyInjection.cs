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

        services.Configure<VoiceOptions>(configuration.GetSection(VoiceOptions.SectionName));



        services.AddSingleton<ILaudosUxHtmlValidator, LaudosUxHtmlValidator>();

        services.AddSingleton<ILaudosUxModoTextoValidator, LaudosUxModoTextoValidator>();

        services.AddSingleton<IVoiceSessionStore, InMemoryVoiceSessionStore>();



        RegisterConversionProvider(services, configuration);

        RegisterVoiceProvider(services, configuration);

        RegisterTranscriptionProvider(services, configuration);



        return services;

    }



    private static void RegisterConversionProvider(IServiceCollection services, IConfiguration configuration)

    {

        var provider = configuration.GetSection(ConversionOptions.SectionName)["Provider"] ?? "Cursor";



        if (!Enum.TryParse<ConversionProvider>(provider, ignoreCase: true, out var conversionProvider))

        {

            conversionProvider = ConversionProvider.Cursor;

        }



        switch (conversionProvider)

        {

            case ConversionProvider.AzureOpenAI:

                services.AddScoped<IImageConversionConverter, AzureOpenAiImageToHtmlConverter>();
                services.AddScoped<IImageMeasureAnalyzer, MockImageToHtmlConverter>();

                break;

            case ConversionProvider.Mock:

                services.AddScoped<IImageConversionConverter, MockImageToHtmlConverter>();
                services.AddScoped<IImageMeasureAnalyzer, MockImageToHtmlConverter>();

                break;

            default:

                services.AddScoped<IImageConversionConverter, CursorComposerImageToHtmlConverter>();
                services.AddScoped<IImageMeasureAnalyzer, CursorComposerImageToHtmlConverter>();

                break;

        }

    }



    private static void RegisterVoiceProvider(IServiceCollection services, IConfiguration configuration)

    {

        var provider = configuration.GetSection(VoiceOptions.SectionName)["Provider"] ?? "Cursor";



        if (string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase))

        {

            services.AddScoped<IVoiceLaudoService, MockVoiceLaudoService>();

        }

        else

        {

            services.AddScoped<IVoiceLaudoService, CursorVoiceLaudoService>();

        }

    }



    private static void RegisterTranscriptionProvider(IServiceCollection services, IConfiguration configuration)

    {

        var provider = configuration.GetSection(VoiceOptions.SectionName)["TranscriptionProvider"] ?? "Whisper";



        if (string.Equals(provider, "Whisper", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ITranscriptionService>(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VoiceOptions>>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WhisperTranscriptionService>>();
                return new WhisperTranscriptionService(options, logger, new HttpClient());
            });
        }

        else

        {

            services.AddSingleton<ITranscriptionService, NoOpTranscriptionService>();

        }

    }

}


