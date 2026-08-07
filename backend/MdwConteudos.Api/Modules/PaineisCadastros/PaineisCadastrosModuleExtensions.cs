namespace MdwConteudos.Api.Modules.PaineisCadastros;

public static class PaineisCadastrosModuleExtensions
{
    public static IServiceCollection AddPaineisCadastros(this IServiceCollection services)
    {
        services.AddScoped<IPaineisCadastrosService, PaineisCadastrosService>();
        return services;
    }
}
