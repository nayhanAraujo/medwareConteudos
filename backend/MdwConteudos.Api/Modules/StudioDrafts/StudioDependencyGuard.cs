using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public sealed record DependencyNode(string Key, string ItemId, int? CodVariavel, string? Token, string Nome,
    string? SectionId, string? SectionName, bool Preserved, IReadOnlyList<string> Dependencies, string? SourceId = null);
public sealed record RemovalDependent(DependencyNode Measure, string RequiredKey, bool Indirect);
public sealed record RemovalBlock(string Operation, IReadOnlyList<DependencyNode> Removed, IReadOnlyList<RemovalDependent> Dependents);
public sealed class MeasureInUseException(RemovalBlock details) : Exception(details.Removed.Count == 1
    ? $"Não é possível remover {details.Removed[0].Nome}." : "Não é possível remover estas medidas: outros cálculos precisam delas.")
{
    public string Code => "measure_in_use";
    public RemovalBlock Details { get; } = details;
}

public static class StudioDependencyGuard
{
    private static string TokenKey(string token) => "token:" + token.ToUpperInvariant();
    private static string CatalogKey(int id) => "catalog:" + id;
    private static string[] Tokens(JsonObject raw) => Regex.Matches(StudioJsonImport.Text(raw, "funcao") + "\n" + StudioJsonImport.Text(raw, "formula"),
        @"(?<![\p{L}\p{N}_])VR_([\p{L}\p{N}_]+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
        .Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<DependencyNode> Graph(StudioDocument document, IReadOnlyList<VariavelOpcao> catalog)
    {
        if (document.ImportedJson is not null)
        {
            var graph = new List<DependencyNode>();
            foreach (var element in StudioJsonImport.Elements(document))
            {
                var section = document.Secoes.FirstOrDefault(s => s.Imported?.Position == element.Position || s.Itens.Any(i => i.Imported?.Position == element.Position));
                var item = section?.Itens.FirstOrDefault(i => i.Imported?.Position == element.Position);
                if (section is null)
                {
                    var parent = StudioJsonImport.Text(element.Properties, "etiquetaPai");
                    section = document.Secoes.FirstOrDefault(s => StudioJsonImport.Text(s.Imported!.Properties, "nome") == parent);
                }
                var token = StudioJsonImport.Text(element.Properties, "nome");
                var key = token.Length > 0 ? TokenKey(token) : "component:" + element.Position;
                var label = StudioJsonImport.Text(element.Properties, "descricao");
                if (string.IsNullOrWhiteSpace(label)) label = item?.Descricao ?? "Componente preservado";
                graph.Add(new(key, item?.Id ?? (section?.Imported?.Position == element.Position ? section.Id : "component:" + element.Position), item?.CodVariavel,
                    token, label, section?.Id, section?.Nome, item is null && section?.Imported?.Position != element.Position,
                    Tokens(element.Properties).Select(TokenKey).Where(k => k != key).ToArray(),element.Properties["id"]?.ToString()));
            }
            return graph;
        }
        var map = catalog.ToDictionary(v => v.CodVariavel);
        return document.Secoes.SelectMany(s => s.Itens.Select(i => {
            map.TryGetValue(i.CodVariavel ?? 0, out var variable);
            return new DependencyNode(i.CodVariavel is { } code ? CatalogKey(code) : "item:" + i.Id, i.Id, i.CodVariavel,
                variable?.Codigo ?? variable?.Sigla, variable?.Nome ?? i.Descricao ?? "Medida sem associação", s.Id, s.Nome, false,
                (variable?.Dependencias ?? []).Where(id => id != i.CodVariavel).Select(CatalogKey).ToArray());
        })).ToArray();
    }

    public static IReadOnlyList<DependencyNode> VoiceGraph(string json, IReadOnlyList<VariavelOpcao> catalog)
    {
        var fields = (JsonNode.Parse(json)?["camposScript"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var tokens = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in catalog) { if (!string.IsNullOrWhiteSpace(v.Codigo)) tokens[v.Codigo] = v.CodVariavel; if(!string.IsNullOrWhiteSpace(v.Sigla))tokens[v.Sigla]=v.CodVariavel; }
        string Key(JsonObject f, int position) { var token = StudioJsonImport.Text(f, "nome"); return tokens.TryGetValue(token, out var id) ? CatalogKey(id) : token.Length > 0 ? TokenKey(token) : "field:" + position; }
        var keys = fields.Select((f,i) => (Name: StudioJsonImport.Text(f, "nome"), Key: Key(f,i))).Where(x => x.Name.Length > 0).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);
        return fields.Select((f,i) => {
            var token = StudioJsonImport.Text(f, "nome"); var key = Key(f,i);
            var variable = tokens.TryGetValue(token, out var id) ? catalog.First(v => v.CodVariavel == id) : null;
            var parent = fields.FirstOrDefault(s => StudioJsonImport.Text(s, "tipo") == "etiqueta" && StudioJsonImport.Text(s, "nome") == StudioJsonImport.Text(f, "etiquetaPai"));
            var deps = variable is not null ? (variable.Dependencias ?? []).Select(CatalogKey) : Tokens(f).Select(t => keys.GetValueOrDefault(t, TokenKey(t)));
            return new DependencyNode(key, f["id"]?.ToString() ?? "field:" + i, variable?.CodVariavel, token,
                variable?.Nome ?? (StudioJsonImport.Text(f, "descricao") is {Length: > 0} label ? label : "Campo do modelo"),
                parent?["id"]?.ToString(), parent is null ? null : StudioJsonImport.Text(parent, "descricao"), StudioJsonImport.Text(f, "tipo") is not ("numero" or "etiqueta"), deps.Where(d => d != key).ToArray(),f["id"]?.ToString());
        }).ToArray();
    }

    public static RemovalBlock? Analyze(IReadOnlyList<DependencyNode> before, IReadOnlyList<DependencyNode> after, string operation)
    {
        var map = before.GroupBy(n => n.Key).ToDictionary(g => g.Key, g => g.First());
        var afterMap = after.GroupBy(n => n.Key).ToDictionary(g => g.Key, g => g.First());
        var remaining = after.Select(n => n.Key).ToHashSet();
        var removed = map.Values.Where(n => !remaining.Contains(n.Key)).ToArray();
        var blocked = new List<DependencyNode>(); var dependents = new List<RemovalDependent>();
        foreach (var target in removed)
        {
            foreach (var source in afterMap.Values)
            {
                var visited = new HashSet<string> {source.Key}; var queue = new Queue<(string Key,int Depth)>();
                foreach (var dependency in source.Dependencies) queue.Enqueue((dependency,1));
                while (queue.TryDequeue(out var step))
                {
                    if (step.Key == target.Key) { dependents.Add(new(map.GetValueOrDefault(source.Key,source),target.Key,step.Depth > 1)); break; }
                    if (!visited.Add(step.Key)) continue;
                    var intermediate=afterMap.GetValueOrDefault(step.Key)??map.GetValueOrDefault(step.Key);if(intermediate is null)continue;
                    foreach (var dependency in intermediate.Dependencies) queue.Enqueue((dependency,step.Depth + 1));
                }
            }
            if (dependents.Any(d => d.RequiredKey == target.Key)) blocked.Add(target);
        }
        return blocked.Count > 0 ? new(operation, blocked, dependents.OrderBy(d => d.Indirect).ThenBy(d => d.Measure.Nome).ToArray()) : null;
    }
    public static void Ensure(StudioDocument before, StudioDocument after, IReadOnlyList<VariavelOpcao> catalog, string operation)
    {
        if (Analyze(Graph(before,catalog), Graph(after,catalog),operation) is { } block) throw new MeasureInUseException(block);
    }
    public static void EnsureVoice(string before, string after, IReadOnlyList<VariavelOpcao> catalog)
    {
        if (Analyze(VoiceGraph(before,catalog),VoiceGraph(after,catalog),"edit") is { } block) throw new MeasureInUseException(block);
    }
}
