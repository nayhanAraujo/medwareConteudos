using Microsoft.Extensions.DependencyInjection;

namespace MdwConteudos.Api.Modules.FormulasModelos;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFormulasModelosModules(this IServiceCollection services)
    {
        services.AddScoped<IFormulasModelosService, FormulasModelosService>();
        return services;
    }
}
