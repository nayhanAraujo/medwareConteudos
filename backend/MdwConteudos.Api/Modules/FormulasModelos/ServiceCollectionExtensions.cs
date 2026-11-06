using Microsoft.Extensions.DependencyInjection;

namespace MdwConteudos.Api.Modules.FormulasModelos;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFormulasModelosModules(this IServiceCollection services)
    {
        services.AddScoped<FormulasModelosService>();
        services.AddScoped<IFormulasModelosService>(sp => sp.GetRequiredService<FormulasModelosService>());
        return services;
    }
}
