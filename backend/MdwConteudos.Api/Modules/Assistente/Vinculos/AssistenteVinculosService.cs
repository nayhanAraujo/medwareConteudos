using System.Data.Common;
using Dapper;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Assistente.Vinculos;

public interface IAssistenteVinculosService
{
    Task<AssistenteVinculosDetalhe?> Get(string domain, int id, CancellationToken ct);
    Task<AssistenteVinculoResumo> Summary(string domain, string? search, string filter, int page, int pageSize, CancellationToken ct);
    Task Set(string domain, int id, string relation, IReadOnlyList<int> ids, IReadOnlyList<SequencedItem>? sequenced, CancellationToken ct);
}

public sealed record SequencedItem(int Id, short Sequencia);

public sealed class AssistenteVinculosService(IAssistantFirebirdConnectionFactory connections) : IAssistenteVinculosService
{
    private sealed record Entity(string Table, string Key, string Name, string? Status = null);
    private sealed record Relation(string Source, string Name, string Title, string Target,
        string? LinkTable = null, string? SourceColumn = null, string? TargetColumn = null,
        string? SequenceColumn = null, string? DirectColumn = null, bool Required = false);

    private static readonly IReadOnlyDictionary<string, Entity> Entities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase)
    {
        ["especialidades"] = new("ESPECIALIDADE", "CODESPECIALIDADE", "DESCRICAO"),
        ["grupos"] = new("GRUPO", "CODGRUPO", "GRUPO", "STATUS"),
        ["frases"] = new("FRASE", "CODFRASE", "TITULO", "STATUS"),
        ["operadoras"] = new("OPERADORA", "CODOPERADORA", "NOMEFANTASIA"),
        ["grupos-operadoras"] = new("GRUPOOPERADORA", "CODGRUPOOPERADORA", "DESCRICAO"),
        ["procedimentos"] = new("PROCEDIMENTO", "CODPROCEDIMENTO", "DESCRICAO_PROCED", "STATUS"),
        ["referencias"] = new("REFERENCIA", "CODREFERENCIA", "DESCRICAO"),
        ["esquemas"] = new("ESQUEMAS", "CODESQUEMA", "DESCRICAO"),
        ["esquemas-fotos"] = new("ESQUEMAFOTOS", "CODESQUEMAFOTOS", "TITULO"),
        ["scripts"] = new("SCRIPTLAUDO", "CODSCRIPTLAUDO", "TITULO", "STATUS"),
        ["paginas-fotos"] = new("PAGFOTOS", "CODPAGFOTOS", "TITULO", "STATUS")
    };

    private static readonly Relation[] Relations =
    [
        new("procedimentos","especialidade","Especialidade","especialidades", DirectColumn:"CODESPECIALIDADE", Required:true),
        new("frases","grupo","Grupo","grupos", DirectColumn:"CODGRUPO", Required:true),
        new("grupos","grupo-pai","Grupo pai","grupos", DirectColumn:"CODGRUPOPAI"),
        new("grupos","especialidades","Especialidades","especialidades","GRUPO_ESPECIALIDADE","CODGRUPO","CODESPECIALIDADE"),
        new("procedimentos","frases","Frases","frases","PROCEDIMENTO_FRASE","CODPROCEDIMENTO","CODFRASE"),
        new("procedimentos","grupos","Grupos","grupos","PROCEDIMENTO_GRUPO","CODPROCEDIMENTO","CODGRUPO"),
        new("procedimentos","scripts","Scripts","scripts","PROCEDIMENTO_SCRIPTLAUDO","CODPROCEDIMENTO","CODSCRIPTLAUDO"),
        new("referencias","especialidades","Especialidades","especialidades","REFERENCIA_ESPECIALIDADE","CODREFERENCIA","CODESPECIALIDADE"),
        new("esquemas-fotos","especialidades","Especialidades","especialidades","ESQUEMAFOTOS_ESPECIALIDADE","CODESQUEMAFOTOS","CODESPECIALIDADE"),
        new("scripts","especialidades","Especialidades","especialidades","SCRIPTLAUDO_ESPECIALIDADE","CODSCRIPTLAUDO","CODESPECIALIDADE"),
        new("scripts","esquemas","Esquemas","esquemas","SCRIPTLAUDO_ESQUEMA","CODSCRIPTLAUDO","CODESQUEMA"),
        new("scripts","paginas-fotos","Modelos MRD","paginas-fotos","SCRIPTLAUDO_PAGFOTOS","CODSCRIPTLAUDO","CODPAGFOTOS","SEQUENCIA"),
        new("paginas-fotos","especialidades","Especialidades","especialidades","PAGFOTOS_ESPECIALIDADE","CODPAGFOTOS","CODESPECIALIDADE"),
        new("paginas-fotos","esquemas","Esquemas","esquemas","PAGFOTOS_ESQUEMA","CODPAGFOTOS","CODESQUEMA"),
        new("grupos-operadoras","operadoras","Operadoras","operadoras","GRUPOOPERADORA_OPERADORA","CODGRUPOOPERADORA","CODOPERADORA")
    ];

    public async Task<AssistenteVinculosDetalhe?> Get(string domain, int id, CancellationToken ct)
    {
        var entity = ResolveEntity(domain);
        await using var c = await connections.OpenConnectionAsync(ct);
        var status = entity.Status is null ? "CAST(NULL AS INTEGER)" : entity.Status;
        var owner = await c.QuerySingleOrDefaultAsync<OwnerRow>(new CommandDefinition(
            $"SELECT {entity.Key} Id, {entity.Name} Nome, {status} Status FROM {entity.Table} WHERE {entity.Key}=@Id",
            new { Id = id }, cancellationToken: ct));
        if (owner is null) return null;

        var groups = new List<AssistenteVinculoGrupo>();
        foreach (var view in ViewsFor(domain))
        {
            var items = await Read(c, view.Relation, view.Inverse, id, ct);
            groups.Add(new(view.Name, view.Title, view.Target, view.Relation.SequenceColumn is not null,
                view.Relation.DirectColumn is not null, view.Relation.Required, !view.Inverse, items));
        }
        return new(domain, id, owner.Nome ?? $"#{id}", owner.Status, groups);
    }

    public async Task<AssistenteVinculoResumo> Summary(string domain, string? search, string filter, int page, int pageSize, CancellationToken ct)
    {
        var entity = ResolveEntity(domain);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var c = await connections.OpenConnectionAsync(ct);
        var searchWhere = string.IsNullOrWhiteSpace(search) ? "" : $"WHERE UPPER(CAST(e.{entity.Name} AS VARCHAR(512))) LIKE @Search";
        var args = new { Search = $"%{search?.Trim().ToUpperInvariant()}%", Skip = (page - 1) * pageSize, Take = pageSize };
        var expressions = ViewsFor(domain).Select(x => CountExpression(x.Relation, x.Inverse, entity, "e")).ToArray();
        var links = expressions.Length == 0 ? "0" : string.Join(" + ", expressions.Select(x => $"({x})"));
        var inner = $"SELECT e.{entity.Key} Id,e.{entity.Name} Nome,{(entity.Status is null ? "CAST(NULL AS INTEGER)" : "e." + entity.Status)} Status,CAST({links} AS INTEGER) TotalVinculos FROM {entity.Table} e {searchWhere}";
        var linkWhere = filter.ToLowerInvariant() switch { "com" => "WHERE q.TotalVinculos > 0", "sem" => "WHERE q.TotalVinculos = 0", _ => "" };
        var total = await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM ({inner}) q {linkWhere}", args, cancellationToken: ct));
        var result = (await c.QueryAsync<AssistenteVinculoResumoItem>(new CommandDefinition(
            $"SELECT q.Id,q.Nome,q.Status,q.TotalVinculos FROM ({inner}) q {linkWhere} ORDER BY q.Nome ROWS @Skip + 1 TO @Skip + @Take",
            args, cancellationToken: ct))).ToList();
        return new(result, total, page, pageSize);
    }

    public async Task Set(string domain, int id, string relation, IReadOnlyList<int> ids, IReadOnlyList<SequencedItem>? sequenced, CancellationToken ct)
    {
        var view = ViewsFor(domain).SingleOrDefault(x => x.Name.Equals(relation, StringComparison.OrdinalIgnoreCase));
        if (view.Relation is null) throw new ArgumentException("Vínculo inválido.");
        if (view.Inverse) throw new InvalidOperationException("Edite este vínculo pelo domínio de origem.");
        var rel = view.Relation;
        var distinct = ids.Distinct().ToArray();
        if (rel.DirectColumn is not null && distinct.Length > 1) throw new InvalidOperationException("Este vínculo aceita apenas um registro.");
        if (rel.Required && distinct.Length == 0) throw new InvalidOperationException("Este vínculo é obrigatório.");
        await using var c = await connections.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        try
        {
            await EnsureExists(c, tx, ResolveEntity(domain), [id], ct);
            await EnsureExists(c, tx, ResolveEntity(rel.Target), distinct, ct);
            if (rel.DirectColumn is not null)
            {
                await c.ExecuteAsync(new CommandDefinition($"UPDATE {ResolveEntity(domain).Table} SET {rel.DirectColumn}=@Target WHERE {ResolveEntity(domain).Key}=@Id",
                    new { Id = id, Target = distinct.Cast<int?>().FirstOrDefault() }, tx, cancellationToken: ct));
            }
            else
            {
                await c.ExecuteAsync(new CommandDefinition($"DELETE FROM {rel.LinkTable} WHERE {rel.SourceColumn}=@Id", new { Id = id }, tx, cancellationToken: ct));
                var order = (sequenced ?? []).ToDictionary(x => x.Id, x => x.Sequencia);
                short next = 0;
                foreach (var child in distinct)
                {
                    var sequenceSql = rel.SequenceColumn is null ? "" : $",{rel.SequenceColumn}";
                    var valueSql = rel.SequenceColumn is null ? "" : ",@Sequence";
                    await c.ExecuteAsync(new CommandDefinition($"INSERT INTO {rel.LinkTable} ({rel.SourceColumn},{rel.TargetColumn}{sequenceSql}) VALUES (@Id,@Child{valueSql})",
                        new { Id = id, Child = child, Sequence = order.GetValueOrDefault(child, next++) }, tx, cancellationToken: ct));
                }
            }
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    private static async Task EnsureExists(DbConnection c, DbTransaction tx, Entity entity, IReadOnlyList<int> ids, CancellationToken ct)
    {
        foreach (var value in ids)
            if (await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM {entity.Table} WHERE {entity.Key}=@Id", new { Id = value }, tx, cancellationToken: ct)) != 1)
                throw new InvalidOperationException($"Registro {value} não existe em {entity.Table}.");
    }

    private static async Task<IReadOnlyList<AssistenteVinculoItem>> Read(DbConnection c, Relation relation, bool inverse, int id, CancellationToken ct)
    {
        var targetDomain = inverse ? relation.Source : relation.Target;
        var target = ResolveEntity(targetDomain);
        var status = target.Status is null ? "CAST(NULL AS INTEGER)" : $"e.{target.Status}";
        string sql;
        if (relation.DirectColumn is not null)
        {
            var source = ResolveEntity(relation.Source);
            sql = inverse
                ? $"SELECT e.{source.Key} Id,e.{source.Name} Nome,{(source.Status is null ? "CAST(NULL AS INTEGER)" : "e." + source.Status)} Status,CAST(NULL AS SMALLINT) Sequencia FROM {source.Table} e WHERE e.{relation.DirectColumn}=@Id ORDER BY e.{source.Name}"
                : $"SELECT e.{target.Key} Id,e.{target.Name} Nome,{status} Status,CAST(NULL AS SMALLINT) Sequencia FROM {source.Table} s JOIN {target.Table} e ON e.{target.Key}=s.{relation.DirectColumn} WHERE s.{source.Key}=@Id";
        }
        else
        {
            var ownerColumn = inverse ? relation.TargetColumn : relation.SourceColumn;
            var linkedColumn = inverse ? relation.SourceColumn : relation.TargetColumn;
            var sequence = relation.SequenceColumn is null ? "CAST(NULL AS SMALLINT)" : $"l.{relation.SequenceColumn}";
            sql = $"SELECT e.{target.Key} Id,e.{target.Name} Nome,{status} Status,{sequence} Sequencia FROM {relation.LinkTable} l JOIN {target.Table} e ON e.{target.Key}=l.{linkedColumn} WHERE l.{ownerColumn}=@Id ORDER BY {(relation.SequenceColumn is null ? "e." + target.Name : "l." + relation.SequenceColumn)}";
        }
        return (await c.QueryAsync<AssistenteVinculoItem>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))).ToList();
    }

    private static string CountExpression(Relation relation, bool inverse, Entity owner, string alias)
    {
        if (relation.DirectColumn is not null)
        {
            if (!inverse) return $"CASE WHEN {alias}.{relation.DirectColumn} IS NULL THEN 0 ELSE 1 END";
            var source = ResolveEntity(relation.Source);
            return $"SELECT COUNT(*) FROM {source.Table} c WHERE c.{relation.DirectColumn}={alias}.{owner.Key}";
        }
        var ownerColumn = inverse ? relation.TargetColumn : relation.SourceColumn;
        return $"SELECT COUNT(*) FROM {relation.LinkTable} l WHERE l.{ownerColumn}={alias}.{owner.Key}";
    }

    private static IEnumerable<(Relation Relation, bool Inverse, string Name, string Title, string Target)> ViewsFor(string domain)
    {
        foreach (var relation in Relations)
        {
            if (relation.Source.Equals(domain, StringComparison.OrdinalIgnoreCase)) yield return (relation, false, relation.Name, relation.Title, relation.Target);
            if (relation.Target.Equals(domain, StringComparison.OrdinalIgnoreCase))
            {
                var sourceTitle = DomainTitle(relation.Source);
                var inverseName = relation.Source;
                if (relation.Source == relation.Target && relation.Name == "grupo-pai") { inverseName = "subgrupos"; sourceTitle = "Subgrupos"; }
                yield return (relation, true, inverseName, sourceTitle, relation.Source);
            }
        }
    }

    private static string DomainTitle(string domain) => domain switch
    {
        "procedimentos" => "Procedimentos", "scripts" => "Scripts", "paginas-fotos" => "Modelos MRD",
        "frases" => "Frases", "grupos" => "Grupos", "especialidades" => "Especialidades",
        "referencias" => "Referências", "esquemas" => "Esquemas", "esquemas-fotos" => "Esquemas de fotos",
        "operadoras" => "Operadoras", "grupos-operadoras" => "Grupos de operadoras", _ => domain
    };
    private static Entity ResolveEntity(string domain) => Entities.TryGetValue(domain, out var value) ? value : throw new ArgumentException("Domínio inválido.");
    private sealed class OwnerRow { public int Id { get; set; } public string? Nome { get; set; } public int? Status { get; set; } }
}
