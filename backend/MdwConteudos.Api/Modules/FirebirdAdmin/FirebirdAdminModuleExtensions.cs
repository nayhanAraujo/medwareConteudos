namespace MdwConteudos.Api.Modules.FirebirdAdmin;

public static class FirebirdAdminModuleExtensions
{
    public static IServiceCollection AddFirebirdAdminModule(this IServiceCollection services)
    {
        services.AddScoped<IFirebirdAdminService, FirebirdAdminService>();
        return services;
    }
}

