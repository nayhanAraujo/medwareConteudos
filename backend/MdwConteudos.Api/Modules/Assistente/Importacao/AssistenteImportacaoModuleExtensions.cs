using Microsoft.Extensions.DependencyInjection;

namespace MdwConteudos.Api.Modules.Assistente.Importacao;

public static class AssistenteImportacaoModuleExtensions
{
    public static IServiceCollection AddAssistenteImportacao(this IServiceCollection services)
    {
        services.AddScoped<IAssistenteImportacaoService, AssistenteImportacaoService>();
        return services;
    }
}
