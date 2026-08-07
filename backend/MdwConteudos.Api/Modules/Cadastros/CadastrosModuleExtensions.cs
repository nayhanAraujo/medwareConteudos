namespace MdwConteudos.Api.Modules.Cadastros;

public static class CadastrosModuleExtensions
{
    public static IServiceCollection AddCadastrosModules(this IServiceCollection services)
    {
        services.AddScoped<ICadastrosService, CadastrosService>();
        return services;
    }
}
