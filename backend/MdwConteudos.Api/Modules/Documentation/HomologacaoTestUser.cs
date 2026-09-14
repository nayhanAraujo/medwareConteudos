using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.Users;

namespace MdwConteudos.Api.Modules.Documentation;

public static class HomologacaoTestUser
{
    // Called only after the guard has validated and frozen both copied database paths.
    public static async Task EnsureAsync(WebApplication app)
    {
        if (!app.Environment.IsEnvironment(HomologacaoGuard.EnvironmentName)) return;
        HomologacaoGuard.Validate(app);
        using var scope = app.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<WebAuthOptions>>().Value;
        if (!options.DevUserEnabled) return;
        var users = scope.ServiceProvider.GetRequiredService<IUsersService>();
        var existing = (await users.ListAsync()).FirstOrDefault(u => u.Identificacao.Equals(options.DevUsername, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (existing.Nome != options.DevName || existing.Perfil != "admin" || existing.Status != -1)
                throw new InvalidOperationException("O login reservado de homologação conflita com um registro da cópia. Use uma cópia sem esse login.");
            return;
        }
        var result = await users.CreateAsync(new CreateUserRequest(options.DevName, options.DevUsername,
            options.DevPassword, options.DevPassword, "admin", -1));
        if (!result.Ok) throw new InvalidOperationException("Não foi possível preparar o usuário exclusivo da homologação.");
    }
}
