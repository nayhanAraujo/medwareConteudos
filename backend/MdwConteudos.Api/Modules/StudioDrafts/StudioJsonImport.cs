using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.StudioDrafts;

// Imported properties are data only. No formula or HTML from a file is executed.
public static class StudioJsonImport
{
    public const int MaxBytes = 1024 * 1024;
    public static string Text(JsonObject node, string key) => node[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
    public static int Number(JsonObject node, string key, int fallback = 0) => int.TryParse(node[key]?.ToString(), out var n) ? n : fallback;
    private static JsonObject Clone(JsonObject o) => (JsonObject)o.DeepClone();
    public static StudioDocument Parse(byte[] bytes, string fileName, IReadOnlyList<VariavelOpcao> catalog)
    {
        if (bytes.Length is 0 or > MaxBytes || !Path.GetExtension(fileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new StudioException(400, "Use um arquivo .json UTF-8 de até 1 MiB.");
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new StudioException(400, "O arquivo deve estar codificado em UTF-8."); }
        // Reject duplicate JSON keys too: a parser must not silently discard source properties.
        using var parsed = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
        CheckKeys(parsed.RootElement);
        if (JsonNode.Parse(text) is not JsonObject root || root["camposScript"] is not JsonArray fields || fields.Count is 0 or > 500)
            throw new StudioException(400, "Informe um objeto com camposScript contendo de 1 a 500 elementos.");
        var objects = fields.Select(n => n as JsonObject ?? throw new StudioException(400, "Cada elemento de camposScript deve ser um objeto.")).ToArray();
        var ids = new HashSet<string>();
        foreach (var o in objects)
            if (o["id"] is { } id && !ids.Add(id.ToJsonString().Trim('"')))
                throw new StudioException(400, "O arquivo contém IDs duplicados.");
        var sections = new List<StudioSection>();
        var byToken = new Dictionary<string, StudioSection>(StringComparer.Ordinal);
        for (var i = 0; i < objects.Length; i++)
        {
            var o = objects[i];
            if (Text(o, "tipo") != "etiqueta") continue;
            var token = Text(o, "nome");
            if (string.IsNullOrWhiteSpace(token) || byToken.ContainsKey(token)) throw new StudioException(400, "As seções devem ter nomes internos únicos e preenchidos.");
            var column = Number(o, "coluna", 1);
            if (column is not (1 or 2)) throw new StudioException(400, "Seções devem estar na coluna 1 ou 2.");
            var label = Text(o, "descricao");
            var section = new StudioSection(StudioDocumentEditor.Token(), StudioDocumentEditor.Title(string.IsNullOrWhiteSpace(label) ? token : label), column, [], Imported: new(i, Clone(o)));
            sections.Add(section); byToken.Add(token, section);
        }
        var components = new List<StudioImportedElement>();
        for (var i = 0; i < objects.Length; i++)
        {
            var o = objects[i]; if (Text(o, "tipo") == "etiqueta") continue;
            var parent = Text(o, "etiquetaPai");
            var element = new StudioImportedElement(i, Clone(o));
            if (!string.IsNullOrEmpty(parent) && !byToken.ContainsKey(parent))
                throw new StudioException(400, $"Seção de destino inexistente no elemento {i + 1}.");
            if (Text(o, "tipo") != "numero" || Number(o, "coluna", 1) == -1 || !byToken.TryGetValue(parent, out var section))
            { components.Add(element); continue; }
            var candidates = Candidates(o, catalog);
            section.Itens.Add(new(StudioDocumentEditor.Token(), candidates.Length == 1 ? candidates[0].CodVariavel : null,
                Text(o, "descricao"), o["geraGrafico"] is JsonValue v && v.TryGetValue<bool>(out var b) && b, Imported: element));
        }
        sections = sections.OrderBy(s => Number(s.Imported!.Properties, "ordem")).ThenBy(s => s.Imported!.Position).ToList();
        foreach (var section in sections) section.Itens.Sort((a,b) => {
            var order = Number(a.Imported!.Properties, "ordem").CompareTo(Number(b.Imported!.Properties, "ordem"));
            return order == 0 ? a.Imported.Position.CompareTo(b.Imported.Position) : order;
        });
        var extras = Clone(root); extras.Remove("camposScript");
        var name = Path.GetFileNameWithoutExtension(fileName); if (name.Length > 120) name = name[..120];
        var doc = new StudioDocument(StudioDocumentEditor.Title(name), sections, ImportedJson: new(extras, components, Path.GetFileName(fileName)));
        StudioDocumentEditor.Shape(doc);
        return doc;
    }
    private static void CheckKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in element.EnumerateObject()) { if (!keys.Add(p.Name)) throw new StudioException(400, "O JSON contém propriedades repetidas."); CheckKeys(p.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) CheckKeys(item);
    }
    public static VariavelOpcao[] Candidates(JsonObject o, IReadOnlyList<VariavelOpcao> catalog)
    {
        var token = Text(o, "nome"); var label = Text(o, "descricao");
        var exact = catalog.Where(v => !string.IsNullOrEmpty(token) && string.Equals(v.Codigo ?? v.Sigla, token, StringComparison.OrdinalIgnoreCase)).ToArray();
        return exact.Length > 0 ? exact : catalog.Where(v => !string.IsNullOrEmpty(label) && string.Equals(v.Nome, label, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    public static IEnumerable<StudioImportedElement> Elements(StudioDocument doc) => doc.Secoes.Select(s => s.Imported).Concat(doc.Secoes.SelectMany(s => s.Itens).Select(i => i.Imported))
        .Where(e => e is not null).Select(e => e!).Concat(doc.ImportedJson?.Components ?? []);
    public static string Export(StudioDocument doc)
    {
        if (doc.ImportedJson is null) throw new StudioException(400, "Documento não importado.");
        var root = Clone(doc.ImportedJson.Root);
        root["camposScript"] = new JsonArray(Elements(doc).OrderBy(e => e.Position).Select(e => (JsonNode)Clone(e.Properties)).ToArray());
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
    public static IReadOnlyList<DraftProblem> Problems(StudioDocument doc)
    {
        var elements = Elements(doc).ToArray(); var names = elements.Select(e => Text(e.Properties, "nome")).Where(n => n.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var problems = new List<DraftProblem>();
        var graph = names.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var e in elements)
        {
            var token = Text(e.Properties, "nome");
            var source = Text(e.Properties, "funcao") + "\n" + Text(e.Properties, "formula");
            foreach (Match match in Regex.Matches(source, @"(?<![\p{L}\p{N}_])VR_([\p{L}\p{N}_]+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                var target = match.Groups[1].Value;
                if (target.Equals(token, StringComparison.OrdinalIgnoreCase)) continue;
                if (!names.Contains(target)) problems.Add(new("import_reference", $"{token}: referência VR_{target} ausente.", doc.Secoes.SelectMany(s => s.Itens).FirstOrDefault(i => i.Imported?.Position == e.Position)?.Id));
                else if (graph.TryGetValue(token, out var refs)) refs.Add(target);
            }
        }
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool Visit(string n) { if (done.Contains(n)) return false; if (!visiting.Add(n)) return true; foreach (var next in graph[n]) if (Visit(next)) return true; visiting.Remove(n); done.Add(n); return false; }
        foreach (var n in names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)) { visiting.Clear(); if (Visit(n)) { problems.Add(new("import_cycle", "Ciclo nas funções importadas: " + n)); break; } }
        return problems.DistinctBy(p => p.Message).ToArray();
    }
    public static void EnsureSafeChange(StudioDocument before, StudioDocument after)
    {
        var existing = Problems(before).Select(p => p.Message).ToHashSet();
        var issue = Problems(after).FirstOrDefault(p => !existing.Contains(p.Message));
        if (issue is not null) throw new StudioException(400, "A alteração criaria uma referência inválida: " + issue.Message);
    }
    private static void SetNumber(JsonObject node, string key, int position)
    {
        node[key] = node[key] is JsonValue v && v.TryGetValue<string>(out _) ? JsonValue.Create(position.ToString()) : JsonValue.Create(position);
    }
    private static void SetOrder(JsonObject node, int position) => SetNumber(node, "ordem", position);
    // Only changed structural attributes are patched; untouched imported properties remain exact.
    public static StudioDocument Synchronize(StudioDocument before, StudioDocument doc, IReadOnlyList<VariavelOpcao> catalog)
    {
        var elements = Elements(doc).ToArray();
        var nextPosition = elements.Select(e => e.Position).DefaultIfEmpty(-1).Max() + 1;
        var nextId = elements.Select(e => Number(e.Properties, "id")).DefaultIfEmpty().Max() + 1;
        var tokens = elements.Select(e => Text(e.Properties, "nome")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sectionOrderChanged = !before.Secoes.Select(s => s.Id).SequenceEqual(doc.Secoes.Take(before.Secoes.Count).Select(s => s.Id));
        var maxSectionOrder = before.Secoes.Select(s => s.Imported is null ? 0 : Number(s.Imported.Properties, "ordem")).DefaultIfEmpty(-1).Max();
        string Token(string prefix) { var token = prefix; while (!tokens.Add(token)) token = prefix + "_" + nextId++; return token; }
        for (var n = 0; n < doc.Secoes.Count; n++)
        {
            var section = doc.Secoes[n]; var previous = before.Secoes.FirstOrDefault(s => s.Id == section.Id);
            var raw = section.Imported;
            if (raw is null) raw = new(nextPosition++, new JsonObject { ["id"] = nextId++, ["tipo"] = "etiqueta", ["nome"] = Token("SECAO_" + nextId), ["descricao"] = section.Nome, ["coluna"] = section.Coluna, ["ordem"] = maxSectionOrder + n - before.Secoes.Count + 1, ["imprimir"] = true });
            if (previous is not null)
            {
                if (section.Nome != previous.Nome) raw.Properties["descricao"] = section.Nome;
                if (section.Coluna != previous.Coluna) SetNumber(raw.Properties, "coluna", section.Coluna);
            }
            if (sectionOrderChanged) SetOrder(raw.Properties, n);
            doc.Secoes[n] = section = section with { Imported = raw };
            var itemOrderChanged = previous is not null && !previous.Itens.Select(i => i.Id).SequenceEqual(section.Itens.Take(previous.Itens.Count).Select(i => i.Id));
            var maxItemOrder = previous?.Itens.Select(i => i.Imported is null ? 0 : Number(i.Imported.Properties, "ordem")).DefaultIfEmpty(-1).Max() ?? -1;
            for (var j = 0; j < section.Itens.Count; j++)
            {
                var item = section.Itens[j]; var oldSection = before.Secoes.FirstOrDefault(s => s.Itens.Any(i => i.Id == item.Id));
                var data = item.Imported;
                if (data is null)
                {
                    var variable = catalog.First(v => v.CodVariavel == item.CodVariavel);
                    var token = variable.Codigo ?? variable.Sigla;
                    if (!tokens.Add(token)) throw new StudioException(400, "Uma medida com esse token já existe no arquivo.");
                    data = new(nextPosition++, new JsonObject { ["id"] = nextId++, ["tipo"] = "numero", ["nome"] = token, ["descricao"] = variable.Nome,
                        ["medida"] = variable.Unidade, ["casasDecimais"] = JsonSerializer.SerializeToNode(variable.CasasDecimais), ["funcao"] = variable.Formula,
                        ["normalidade"] = variable.Normalidade, ["classificacoes"] = variable.Classificacoes, ["geraGrafico"] = item.ExibirGrafico,
                        ["etiquetaPai"] = Text(raw.Properties, "nome"), ["coluna"] = section.Coluna, ["ordem"] = maxItemOrder + j - (previous?.Itens.Count ?? 0) + 1, ["linha"] = j + 1, ["imprimir"] = true });
                }
                if (oldSection is not null)
                {
                    if (oldSection.Id != section.Id) data.Properties["etiquetaPai"] = Text(raw.Properties, "nome");
                    if (oldSection.Coluna != section.Coluna) SetNumber(data.Properties, "coluna", section.Coluna);
                    if (oldSection.Id != section.Id && !itemOrderChanged) SetOrder(data.Properties, maxItemOrder + j - (previous?.Itens.Count ?? 0) + 1);
                }
                if (itemOrderChanged) SetOrder(data.Properties, j);
                section.Itens[j] = item with { Imported = data };
            }
        }
        // Auxiliary components follow their parent section when that section changes columns.
        foreach (var component in doc.ImportedJson!.Components)
        {
            var parent = Text(component.Properties, "etiquetaPai");
            var section = doc.Secoes.FirstOrDefault(s => Text(s.Imported!.Properties, "nome") == parent);
            if (section is null && before.Secoes.Any(s => Text(s.Imported!.Properties, "nome") == parent))
                throw new StudioException(400, "A seção contém componentes preservados e não pode ser excluída nesta versão.");
            var previous = before.Secoes.FirstOrDefault(s => s.Id == section?.Id);
            if (section is not null && previous is not null && previous.Coluna != section.Coluna && Number(component.Properties, "coluna", -1) != -1)
                SetNumber(component.Properties, "coluna", section.Coluna);
        }
        var affectedColumns = new HashSet<int>();
        foreach (var section in before.Secoes.Concat(doc.Secoes).DistinctBy(s => s.Id))
        {
            var old = before.Secoes.FirstOrDefault(s => s.Id == section.Id); var current = doc.Secoes.FirstOrDefault(s => s.Id == section.Id);
            if (old is null || current is null || old.Coluna != current.Coluna || before.Secoes.IndexOf(old) != doc.Secoes.IndexOf(current)
                || !old.Itens.Select(i => i.Id).SequenceEqual(current.Itens.Select(i => i.Id)))
            { if (old is not null) affectedColumns.Add(old.Coluna); if (current is not null) affectedColumns.Add(current.Coluna); }
        }
        foreach (var column in affectedColumns)
        {
            var row = 0;
            foreach (var section in doc.Secoes.Where(s => s.Coluna == column))
            {
                SetNumber(section.Imported!.Properties, "linha", row++);
                var token = Text(section.Imported.Properties, "nome");
                var children = section.Itens.Select(i => i.Imported!).Concat(doc.ImportedJson.Components.Where(c => Text(c.Properties, "etiquetaPai") == token && Number(c.Properties, "coluna", -1) != -1));
                foreach (var child in children.OrderBy(c => Number(c.Properties, "ordem")).ThenBy(c => c.Position)) SetNumber(child.Properties, "linha", row++);
            }
        }
        EnsureSafeChange(before, doc);
        return doc;
    }
}
