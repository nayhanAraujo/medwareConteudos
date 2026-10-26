using MdwConteudos.Api.Modules.Avaliacoes;
using MdwConteudos.Api.Modules.Auth;
using MdwConteudos.Api.Modules.Auditoria;
using MdwConteudos.Api.Modules.Orcamentos;
using MdwConteudos.Api.Modules.Cadastros;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Modules.Web;

namespace MdwConteudos.Api.Infrastructure;

public static class DatabaseMigrations
{
    // Register every repeatable, backward-compatible deployment migration here, in dependency order.
    // Destructive changes require a separate deployment/restore plan.
    public static async Task RunAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<IFirebirdConnectionFactory>();
        var permissions = services.GetRequiredService<IPermissionService>();
        var steps = new (string Name, Func<Task> Run)[]
        {
            ("permissions", () => permissions.EnsureSchemaAsync(ct)),
            ("password-recovery", () => PasswordRecoverySchema.MigrateAsync(db, ct)),
            ("package-icons", () => PacoteIcone.MigrateAsync(db, ct)),
            ("report-module-icons", () => ModuloRelatorioIcone.MigrateAsync(db, ct)),
            ("evaluations", () => AvaliacaoSchema.MigrateAsync(db, ct)),
            ("orcamentos", () => OrcamentoSchema.MigrateAsync(db, ct)),
            ("audit", () => AuditSchema.MigrateAsync(db, ct))
        };
        foreach (var step in steps)
        {
            logger.LogInformation("[Migration] Iniciando {Migration}", step.Name);
            await step.Run();
            logger.LogInformation("[Migration] Concluída {Migration}", step.Name);
        }

        await permissions.ValidateSchemaAsync(ct);
        await AuditSchema.ValidateAsync(db, ct);
        foreach (var entry in await AuditSchema.CoverageAsync(db, ct))
            logger.LogInformation("[Migration] Auditoria Table={Table} Present={Present} Installed={Installed}", entry.Table, entry.Present, entry.Installed);
        await using var connection = await db.OpenConnectionAsync(ct);
        await AvaliacaoSchema.ValidateAsync(connection);
        logger.LogInformation("[Migration] Banco preparado e catálogo de permissões validado.");
    }
}
