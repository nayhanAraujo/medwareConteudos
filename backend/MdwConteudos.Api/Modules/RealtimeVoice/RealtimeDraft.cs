using System.Text.Json;
using System.Text.Json.Nodes;
using ConversorHtml.Application.Services;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.RealtimeVoice;

/// <summary>Structured, reversible edits. No model-generated JSON replaces the draft.</summary>
public sealed class RealtimeDraft
{
    private JsonObject _root;
    private readonly List<Edit> _history = [];
    public long Revision { get; private set; }
    public string Json => _root.ToJsonString();
    private JsonArray Fields => _root["camposScript"]!.AsArray();
    private sealed record Edit(string Group, Dictionary<int, JsonNode?> Before, Dictionary<int, JsonNode?> After);

    public RealtimeDraft(string json)
    {
        _root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Rascunho inválido.");
        if (_root["camposScript"] is not JsonArray fields) throw new InvalidOperationException("Rascunho inválido.");
        // Assign stable identities also to legacy voice models.
        var ids = new HashSet<int>();
        var next = fields.Count + 1;
        foreach (var field in fields)
        {
            var id = field?["id"]?.GetValue<int>() ?? 0;
            if (id <= 0 || !ids.Add(id)) { while (!ids.Add(next)) next++; field!["id"] = next++; }
        }
    }

    private Dictionary<int, JsonNode?> Snapshot() => Fields.ToDictionary(f => f!["id"]!.GetValue<int>(), f => f!.DeepClone());
    public object Apply(string name, JsonElement args, IReadOnlyList<VariavelOpcao> catalog)
    {
        var allowed = name switch
        {
            "criar_secao" => new[] { "grupoOperacao", "substituirOperacao", "nome", "coluna", "ordem" },
            "organizar_secao" => ["grupoOperacao", "substituirOperacao", "secaoId", "nome", "coluna", "ordem"],
            "incluir_medidas" => ["grupoOperacao", "substituirOperacao", "secaoId", "codVariaveis"],
            "mover_medida" => ["grupoOperacao", "substituirOperacao", "campoId", "secaoId", "ordem"],
            "desfazer_alteracao" => ["grupoOperacao"],
            _ => throw new InvalidOperationException("Ferramenta não permitida.")
        };
        if (args.ValueKind != JsonValueKind.Object || args.EnumerateObject().Any(p => !allowed.Contains(p.Name)))
            throw new InvalidOperationException("Argumentos inválidos.");
        var group = args.GetProperty("grupoOperacao").GetString();
        if (string.IsNullOrWhiteSpace(group) || group.Length > 100) throw new InvalidOperationException("Grupo inválido.");
        var original = _root.DeepClone().AsObject();
        var history = _history.ToArray();
        try
        {
            if (name == "desfazer_alteracao")
            {
                Undo(group);
                ValidateDependencies(catalog);
                Revision++;
                return new { revision = Revision };
            }
            if (args.TryGetProperty("substituirOperacao", out var replace) && replace.GetBoolean()) Undo(group);
            var before = Snapshot();
            if (name == "criar_secao")
            {
                var title = Title(args.GetProperty("nome").GetString());
                var id = Fields.Select(f => f!["id"]!.GetValue<int>()).DefaultIfEmpty().Max() + 1;
                Fields.Add(new JsonObject { ["id"] = id, ["tipo"] = "etiqueta", ["nome"] = $"SECAO_{id}", ["descricao"] = title,
                    ["coluna"] = Column(args), ["ordem"] = Order(args), ["imprimir"] = true });
            }
            else
            {
                var section = Section(args.GetProperty("secaoId").GetInt32());
                if (name == "organizar_secao")
                {
                    if (args.TryGetProperty("nome", out var title)) section["descricao"] = Title(title.GetString());
                    section["coluna"] = Column(args);
                    section["ordem"] = Order(args);
                    foreach (var field in Fields.Where(f => f?["etiquetaPai"]?.GetValue<string>() == section["nome"]!.GetValue<string>()))
                        field!["coluna"] = section["coluna"]!.DeepClone();
                }
                else if (name == "mover_medida")
                {
                    var field = Fields.FirstOrDefault(f => f?["id"]?.GetValue<int>() == args.GetProperty("campoId").GetInt32()
                        && f?["tipo"]?.GetValue<string>() != "etiqueta") ?? throw new InvalidOperationException("Campo não encontrado.");
                    field["etiquetaPai"] = section["nome"]!.DeepClone();
                    field["coluna"] = section["coluna"]!.DeepClone();
                    field["ordem"] = Order(args);
                }
                else
                {
                    var ids = args.GetProperty("codVariaveis").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                    if (ids.Length is < 1 or > 30) throw new InvalidOperationException("Selecione de 1 a 30 medidas.");
                    var map = catalog.ToDictionary(x => x.CodVariavel);
                    var present = Fields.Where(f => f?["tipo"]?.GetValue<string>() != "etiqueta")
                        .Select(f => f?["codVariavel"]?.GetValue<int>() ?? catalog.FirstOrDefault(v =>
                            string.Equals(v.Codigo ?? v.Sigla, f?["nome"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase))?.CodVariavel ?? 0)
                        .Where(id => id > 0).ToHashSet();
                    if (ids.Any(id => present.Contains(id))) throw new InvalidOperationException("Medida já incluída. Use mover_medida.");
                    // Validate the entire reachable graph, including already present dependencies.
                    var ordered = ModeloModoTextoRules.ResolveDependencyOrder(ids, catalog.ToDictionary(v => v.CodVariavel, v => v.Dependencias ?? []));
                    foreach (var id in ordered)
                    {
                        if (!map.TryGetValue(id, out var v) || v.DependenciasNaoEncontradas?.Count > 0)
                            throw new InvalidOperationException("Dependência de fórmula não encontrada.");
                        if (present.Contains(id)) continue;
                        var fieldId = Fields.Select(f => f!["id"]!.GetValue<int>()).DefaultIfEmpty().Max() + 1;
                        Fields.Add(new JsonObject { ["id"] = fieldId, ["codVariavel"] = id, ["tipo"] = "numero",
                            ["nome"] = v.Codigo ?? v.Sigla, ["descricao"] = v.Nome, ["medida"] = v.Unidade,
                            ["formula"] = v.Formula, ["casasDecimais"] = v.CasasDecimais,
                            ["normalidade"] = v.Normalidade, ["classificacoes"] = v.Classificacoes,
                            ["etiquetaPai"] = section["nome"]!.DeepClone(), ["coluna"] = section["coluna"]!.DeepClone(),
                            ["ordem"] = Fields.Count + 1, ["imprimir"] = true });
                        present.Add(id);
                    }
                }
            }
            ValidateDependencies(catalog);
            if (Fields.Count > 500) throw new InvalidOperationException("Limite de 500 itens por rascunho atingido.");
            var after = Snapshot();
            var changedIds = before.Keys.Union(after.Keys).Where(id => !JsonNode.DeepEquals(before.GetValueOrDefault(id), after.GetValueOrDefault(id))).ToHashSet();
            _history.Add(new Edit(group, before.Where(p => changedIds.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
                after.Where(p => changedIds.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value)));
            if (_history.Count > 100) _history.RemoveAt(0);
            Revision++;
            return new { revision = Revision, fields = Fields.Count };
        }
        catch { _root = original; _history.Clear(); _history.AddRange(history); throw; }
    }

    private void Undo(string group)
    {
        var edit = _history.LastOrDefault(h => h.Group == group) ?? throw new InvalidOperationException("Operação não encontrada no histórico.");
        var current = Snapshot();
        var changed = edit.Before.Keys.Union(edit.After.Keys).Where(id => !JsonNode.DeepEquals(edit.Before.GetValueOrDefault(id), edit.After.GetValueOrDefault(id))).ToArray();
        if (changed.Any(id => !JsonNode.DeepEquals(current.GetValueOrDefault(id), edit.After.GetValueOrDefault(id))))
            throw new InvalidOperationException("A operação foi modificada posteriormente; desfazer sobrescreveria outra edição.");
        foreach (var id in changed)
        {
            var old = Fields.FirstOrDefault(f => f?["id"]?.GetValue<int>() == id);
            if (edit.Before.GetValueOrDefault(id) is null && old?["tipo"]?.GetValue<string>() == "etiqueta"
                && Fields.Any(f => f?["etiquetaPai"]?.GetValue<string>() == old["nome"]?.GetValue<string>() && !changed.Contains(f!["id"]!.GetValue<int>())))
                throw new InvalidOperationException("A seção recebeu campos depois desta operação. Mova-os antes de desfazer sua criação.");
            if (old is not null) Fields.Remove(old);
            if (edit.Before.GetValueOrDefault(id) is { } field) Fields.Add(field.DeepClone());
        }
        _history.Remove(edit);
    }

    private void ValidateDependencies(IReadOnlyList<VariavelOpcao> catalog)
    {
        if (catalog.Count == 0) return;
        var ids = Fields.Where(f => f?["tipo"]?.GetValue<string>() != "etiqueta")
            .Select(f => f?["codVariavel"]?.GetValue<int>() ?? catalog.FirstOrDefault(v =>
                string.Equals(v.Codigo ?? v.Sigla, f?["nome"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase))?.CodVariavel ?? 0)
            .Where(id => id > 0).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new InvalidOperationException("Medida duplicada no rascunho.");
        var map = catalog.ToDictionary(v => v.CodVariavel);
        if (ids.Any(id => !map.ContainsKey(id) || map[id].DependenciasNaoEncontradas?.Count > 0))
            throw new InvalidOperationException("Medida ou dependência não encontrada no catálogo.");
        var required = ModeloModoTextoRules.ResolveDependencyOrder(ids, catalog.ToDictionary(v => v.CodVariavel, v => v.Dependencias ?? []));
        if (required.Except(ids).Any()) throw new InvalidOperationException("A edição removeria uma dependência usada por outra medida.");
    }

    public object Describe() => Fields.Select(f => new { id = f!["id"]!.GetValue<int>(), nome = f["nome"]?.GetValue<string>(),
        descricao = f["descricao"]?.GetValue<string>(), tipo = f["tipo"]?.GetValue<string>(), secao = f["etiquetaPai"]?.GetValue<string>(),
        coluna = f["coluna"]?.GetValue<int>(), ordem = f["ordem"]?.ToString() }).ToArray();

    public string Render()
    {
        var fields = Fields.Select(f => f!).ToArray();
        var result = new JsonArray();
        foreach (var s in fields.Where(f => f["tipo"]?.GetValue<string>() == "etiqueta")
                     .OrderBy(f => f["coluna"]?.GetValue<int>() ?? 1).ThenBy(NumberOrder))
        {
            result.Add(s.DeepClone());
            foreach (var f in fields.Where(f => f["etiquetaPai"]?.GetValue<string>() == s["nome"]?.GetValue<string>()).OrderBy(NumberOrder))
                result.Add(f.DeepClone());
        }
        foreach (var f in fields.Where(f => f["tipo"]?.GetValue<string>() != "etiqueta" && !fields.Any(s => s["tipo"]?.GetValue<string>() == "etiqueta" && s["nome"]?.GetValue<string>() == f["etiquetaPai"]?.GetValue<string>())))
            result.Add(f.DeepClone());
        var root = _root.DeepClone().AsObject(); root["camposScript"] = result;
        return root.ToJsonString();
    }

    public SalvarComposicaoRequest Composition(string name)
    {
        var sections = Fields.Where(f => f?["tipo"]?.GetValue<string>() == "etiqueta").ToArray();
        var linked = new HashSet<int>();
        var result = sections.Select(s => new ComposicaoSecaoRequest(null, s!["descricao"]!.GetValue<string>(), s["coluna"]?.GetValue<int>() ?? 1,
            NumberOrder(s), Fields.Where(f => f?["tipo"]?.GetValue<string>() != "etiqueta" && f?["etiquetaPai"]?.GetValue<string>() == s["nome"]?.GetValue<string>())
                .Select(f => { var id = f!["codVariavel"]?.GetValue<int>() ?? 0; if (id <= 0 || !linked.Add(id)) throw new InvalidOperationException("Há campos sem vínculo com o catálogo ou duplicados.");
                    return new ComposicaoVariavelRequest(id, false, NumberOrder(f)); }).ToArray())).ToArray();
        if (result.Length == 0 || linked.Count != Fields.Count(f => f?["tipo"]?.GetValue<string>() != "etiqueta"))
            throw new InvalidOperationException("Todo campo deve pertencer a uma seção e estar vinculado ao catálogo.");
        return new SalvarComposicaoRequest(Title(name), result);
    }

    private JsonNode Section(int id) => Fields.FirstOrDefault(f => f?["id"]?.GetValue<int>() == id && f?["tipo"]?.GetValue<string>() == "etiqueta")
        ?? throw new InvalidOperationException("Seção não encontrada.");
    private static int NumberOrder(JsonNode f) => int.TryParse(f["ordem"]?.ToString(), out var order) ? order : 0;
    private static int Order(JsonElement args) { var n = args.GetProperty("ordem").GetInt32(); return n is >= 0 and <= 10000 ? n : throw new InvalidOperationException("Ordem inválida."); }
    private static int Column(JsonElement args) { var n = args.GetProperty("coluna").GetInt32(); return n is 1 or 2 ? n : throw new InvalidOperationException("Coluna inválida."); }
    private static string Title(string? s) => !string.IsNullOrWhiteSpace(s) && s.Trim().Length <= 120 ? s.Trim() : throw new InvalidOperationException("Nome inválido.");
}
