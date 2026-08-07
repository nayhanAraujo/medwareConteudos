namespace MdwConteudos.Api.Modules.PaineisComplementos;

public static class PaineisComplementosModuleExtensions
{
    public static IServiceCollection AddPaineisComplementos(this IServiceCollection services)
    {
        services.AddScoped<IPaineisVersoesService, PaineisVersoesService>();
        return services;
    }
}
