using System.Data.Common;
using Dapper;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed partial class OrcamentoService
{
    private sealed class TechnicalRow { public long Id { get; set; } public string Document { get; set; } = ""; }
    private static async Task<(string Version, QuoteTechnicalCatalog Catalog)> TechnicalCatalog(DbConnection c, DbTransaction? tx, CancellationToken ct)
    {
        var row = await c.QuerySingleAsync<TechnicalRow>(new CommandDefinition("SELECT v.CODORCAMENTOREGRAVERSAO AS Id,v.DOCUMENTO AS Document FROM ORCAMENTOREGRAVERSAO v JOIN ORCAMENTOREGRACONFIG f ON f.CODORCAMENTOREGRAVERSAO=v.CODORCAMENTOREGRAVERSAO WHERE f.CODORCAMENTOREGRACONFIG=1", transaction: tx, cancellationToken: ct));
        return (row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), OrcamentoJson.Read<QuoteTechnicalCatalog>(row.Document));
    }
    private static async Task<List<QuoteTechnology>> Technologies(DbConnection c, DbTransaction? tx, CancellationToken ct)
    {
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='TIPOLINGUAGEM'", transaction: tx, cancellationToken: ct)) == 0) return [];
        return (await c.QueryAsync<QuoteTechnology>(new CommandDefinition("SELECT CODLINGUAGEM AS Id,NOME AS Name FROM TIPOLINGUAGEM ORDER BY NOME", transaction: tx, cancellationToken: ct))).ToList();
    }
    public async Task<object> GetTechnicalCatalog(CancellationToken ct, bool applicableOnly = false)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot, ct);
        _ = await Actor(c, tx, ct); var (version, catalog) = await TechnicalCatalog(c, tx, ct); var technologies = await Technologies(c, tx, ct); await tx.CommitAsync(ct);
        if (applicableOnly) catalog = catalog with { Profiles = catalog.Profiles.Where(p => p.Active).ToList(), Services = catalog.Services.Where(s => s.Active).ToList(), Rules = catalog.Rules.Where(r => r.Active).ToList() };
        return new { success = true, data = new { version, catalog, technologies } };
    }
    public async Task PublishTechnicalCatalog(QuoteCatalogPublication request, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct);
        QuoteCalculator.CheckText(request.Reason, "Motivo", 1000); if (string.IsNullOrWhiteSpace(request.Reason)) throw new OrcamentoException(422, "Informe o motivo da publicação.");
        var old = await TechnicalCatalog(c, tx, ct); if (old.Version != request.Version) throw new OrcamentoException(409, "Catálogo alterado. Atualize antes de publicar.");
        QuoteTechnicalRules.Validate(request.Catalog, await Technologies(c, tx, ct));
        foreach (var (previous, next) in new[] { (old.Catalog.Profiles, request.Catalog.Profiles), (old.Catalog.Services, request.Catalog.Services) })
            if (previous.Any(p => !next.Any(n => n.Id == p.Id))) throw new OrcamentoException(422, "Inative perfis/serviços em vez de removê-los.");
        if (old.Catalog.Rules.Any(r => !request.Catalog.Rules.Any(n => n.Id == r.Id))) throw new OrcamentoException(422, "Inative regras em vez de removê-las.");
        foreach (var rule in old.Catalog.Rules)
            if (rule.Checks.Any(check => !request.Catalog.Rules.Single(r => r.Id == rule.Id).Checks.Any(n => n.Id == check.Id))) throw new OrcamentoException(422, "Não remova códigos de verificações existentes; publique outra regra inativando a anterior.");
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO ORCAMENTOREGRAVERSAO(DOCUMENTO,MOTIVO,CODUSUARIO,DTHRCRIACAO) VALUES(@json,@reason,@actor,@now) RETURNING CODORCAMENTOREGRAVERSAO", new { json = QuoteTextBlob.Json(request.Catalog), reason = request.Reason, actor = actor.Id, now = Now }, tx, cancellationToken: ct));
        if (await c.ExecuteAsync(new CommandDefinition("UPDATE ORCAMENTOREGRACONFIG SET CODORCAMENTOREGRAVERSAO=@id WHERE CODORCAMENTOREGRACONFIG=1 AND CODORCAMENTOREGRAVERSAO=@previous", new { id, previous = Version(request.Version) }, tx, cancellationToken: ct)) != 1) throw new OrcamentoException(409, "Publicação concorrente. Atualize o catálogo.");
        await Audit(c, tx, "ORCAMENTOREGRAVERSAO", id.ToString(System.Globalization.CultureInfo.InvariantCulture), "editar", ct); await tx.CommitAsync(ct);
    }
}
