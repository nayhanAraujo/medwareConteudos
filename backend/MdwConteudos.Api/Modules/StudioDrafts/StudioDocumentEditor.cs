using System.Text.Json;
using ConversorHtml.Application.Services;
using MdwConteudos.Api.Modules.FormulasModelos;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public static class StudioDocumentEditor
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    public static string Token() => Guid.NewGuid().ToString();
    public static string Title(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 120 ? value.Trim() : throw new StudioException(400, "Informe um nome de até 120 caracteres.");
    public static StudioDocument Apply(StudioDocument original, DraftCommand command, IReadOnlyList<VariavelOpcao> catalog)
    {
        if (original.ImportedJson is not null && command.Type == "importSections") throw new StudioException(400, "Importação por imagem não pode ser mesclada a este JSON.");
        if (original.ImportedJson is not null && command.Type == "editItem" && (command.Payload.TryGetProperty("normality", out _) || command.Payload.TryGetProperty("chart", out _)))
            throw new StudioException(400, "As configurações importadas são somente leitura. É permitido apenas associar a medida.");
        var doc = Copy(original);
        var p = command.Payload;
        string Text(string key) => p.GetProperty(key).GetString() ?? "";
        StudioSection Section(string key = "sectionId") => doc.Secoes.FirstOrDefault(s => s.Id == Text(key)) ?? throw new StudioException(400, "Seção não encontrada.");
        switch (command.Type)
        {
            case "rename": doc = doc with { Nome = Title(Text("name")) }; break;
            case "createSection":
                var col = p.GetProperty("column").GetInt32();
                if (col is not (1 or 2)) throw new StudioException(400, "Coluna inválida.");
                doc.Secoes.Add(new(Token(), Title(Text("name")), col, [])); break;
            case "editSection":
                var section = Section(); var index = doc.Secoes.IndexOf(section);
                col = p.GetProperty("column").GetInt32();
                if (col is not (1 or 2)) throw new StudioException(400, "Coluna inválida.");
                doc.Secoes.Remove(section);
                doc.Secoes.Insert(Math.Clamp(p.GetProperty("position").GetInt32(), 0, doc.Secoes.Count), section with { Nome = Title(Text("name")), Coluna = col }); break;
            case "deleteSection": doc.Secoes.Remove(Section()); break;
            case "clearSection": Section().Itens.Clear(); break;
            case "addMeasures":
                var target = Section();
                var requested = p.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).Distinct().ToArray();
                if (requested.Length is < 1 or > 500) throw new StudioException(400, "Quantidade inválida de medidas.");
                var map = catalog.ToDictionary(v => v.CodVariavel);
                var existing = doc.Secoes.SelectMany(s => s.Itens).Where(i => i.CodVariavel.HasValue).Select(i => i.CodVariavel!.Value).ToHashSet();
                if (doc.ImportedJson is not null)
                {
                    var tokens = StudioJsonImport.Elements(doc).Select(e => StudioJsonImport.Text(e.Properties, "nome")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var v in catalog.Where(v => tokens.Contains(v.Codigo ?? v.Sigla))) existing.Add(v.CodVariavel);
                }
                var order = ModeloModoTextoRules.ResolveDependencyOrder(requested, catalog.ToDictionary(v => v.CodVariavel, v => doc.ImportedJson is not null && existing.Contains(v.CodVariavel) ? (IReadOnlyList<int>)Array.Empty<int>() : v.Dependencias ?? []));
                foreach (var id in order)
                {
                    if (!map.TryGetValue(id, out var variable) || (!existing.Contains(id) && variable.DependenciasNaoEncontradas?.Count > 0)) throw new StudioException(400, "Medida ou dependência não encontrada.");
                    if (existing.Add(id)) target.Itens.Add(new(Token(), id));
                }
                break;
            case "moveItem":
                var itemId = Text("itemId"); target = Section();
                var source = doc.Secoes.FirstOrDefault(s => s.Itens.Any(i => i.Id == itemId)) ?? throw new StudioException(400, "Campo não encontrado.");
                var item = source.Itens.First(i => i.Id == itemId); source.Itens.Remove(item);
                target.Itens.Insert(Math.Clamp(p.GetProperty("position").GetInt32(), 0, target.Itens.Count), item); break;
            case "deleteItem":
                itemId = Text("itemId"); source = doc.Secoes.FirstOrDefault(s => s.Itens.Any(i => i.Id == itemId)) ?? throw new StudioException(400, "Campo não encontrado.");
                source.Itens.RemoveAll(i => i.Id == itemId); break;
            case "editItem":
                itemId = Text("itemId"); source = doc.Secoes.FirstOrDefault(s => s.Itens.Any(i => i.Id == itemId)) ?? throw new StudioException(400, "Campo não encontrado.");
                item = source.Itens.First(i => i.Id == itemId);
                var cod = p.TryGetProperty("codVariavel", out var code) ? code.ValueKind == JsonValueKind.Null ? (int?)null : code.GetInt32() : item.CodVariavel;
                if (cod.HasValue && !catalog.Any(v => v.CodVariavel == cod)) throw new StudioException(400, "Medida não encontrada.");
                source.Itens[source.Itens.IndexOf(item)] = item with { CodVariavel = cod,
                    ExibirGrafico = p.TryGetProperty("chart", out var chart) ? chart.GetBoolean() : item.ExibirGrafico,
                    NormalidadeOverride = p.TryGetProperty("normality", out var normality) ? normality.GetString() : item.NormalidadeOverride }; break;
            case "importSections":
                var incoming = p.GetProperty("sections").Deserialize<List<StudioSection>>(Json) ?? [];
                foreach (var s in incoming) doc.Secoes.Add(new(Token(), Title(s.Nome), s.Coluna, s.Itens.Select(i => i with { Id = Token() }).ToList()));
                break;
            default: throw new StudioException(400, "Comando não permitido.");
        }
        if (doc.ImportedJson is not null) doc = StudioJsonImport.Synchronize(original, doc, catalog);
        Shape(doc);
        var structural = Validate(doc, catalog).FirstOrDefault(e => e.Code is "duplicate" or "cycle");
        if (structural is not null) throw new StudioException(400, structural.Message);
        return doc;
    }

    public static void Shape(StudioDocument document)
    {
        Title(document.Nome);
        if (document.ImportedJson is not null && StudioJsonImport.Elements(document).Count() > 500) throw new StudioException(400, "Limite de 500 elementos importados.");
        if (document.Secoes.Count > 100 || document.Secoes.Sum(s => s.Itens.Count) > 500) throw new StudioException(400, "Limite de 100 seções e 500 campos.");
        var ids = document.Secoes.Select(s => s.Id).Concat(document.Secoes.SelectMany(s => s.Itens).Select(i => i.Id)).ToArray();
        if (ids.Any(s => !Guid.TryParse(s, out _)) || ids.Distinct().Count() != ids.Length || document.Secoes.Any(s => s.Coluna is not (1 or 2)))
            throw new StudioException(400, "Identificadores ou colunas inválidos.");
        foreach (var s in document.Secoes) Title(s.Nome);
        if (document.Secoes.SelectMany(s => s.Itens).Any(i => i.NormalidadeOverride?.Length > 8192 || i.Descricao?.Length > 255)
            || System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(document, Json)) > (document.ImportedJson is null ? 1 : 2) * 1024 * 1024) throw new StudioException(400, "Conteúdo do rascunho excede os limites permitidos.");
    }

    public static IReadOnlyList<DraftProblem> Validate(StudioDocument document, IReadOnlyList<VariavelOpcao> catalog)
    {
        if (document.ImportedJson is not null) return StudioJsonImport.Problems(document)
            .Concat(document.Secoes.SelectMany(s => s.Itens).Where(i => !i.CodVariavel.HasValue).Select(i => new DraftProblem("import_unlinked", "Campo preservado sem associação ao catálogo.", i.Id))).ToArray();
        var errors = new List<DraftProblem>(); var used = new HashSet<int>(); var map = catalog.ToDictionary(v => v.CodVariavel);
        if (document.Secoes.Count == 0) errors.Add(new("empty", "Inclua uma seção."));
        if (document.Secoes.GroupBy(s => s.Nome.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) errors.Add(new("section_name", "Existem seções com nomes repetidos."));
        foreach (var item in document.Secoes.SelectMany(s => s.Itens))
        {
            if (item.CodVariavel is not { } id || !map.ContainsKey(id)) { errors.Add(new("unlinked", "Associe o campo a uma medida do catálogo.", item.Id)); continue; }
            if (!used.Add(id)) errors.Add(new("duplicate", "Medida duplicada.", item.Id));
            if (map[id].DependenciasNaoEncontradas?.Count > 0) errors.Add(new("dependency", "Referência de fórmula inexistente.", item.Id));
        }
        try
        {
            var required = ModeloModoTextoRules.ResolveDependencyOrder(used, catalog.ToDictionary(v => v.CodVariavel, v => v.Dependencias ?? []));
            if (required.Except(used).Any()) errors.Add(new("dependency", "Existem dependências ausentes no rascunho."));
        }
        catch (InvalidOperationException) { errors.Add(new("cycle", "Ciclo nas fórmulas das medidas.")); }
        return errors;
    }

    public static SalvarComposicaoRequest Composition(StudioDocument d, bool copy = false) => d.ImportedJson is not null ? throw new StudioException(400, "Modelos importados podem ser salvos como rascunho e exportados em JSON. A publicação perderia componentes.") : new(d.Nome,
        d.Secoes.Select((s, n) => new ComposicaoSecaoRequest(copy ? null : s.CodSecao, s.Nome, s.Coluna,
            d.Secoes.Take(n + 1).Count(x => x.Coluna == s.Coluna), s.Itens.Select((i, index) => new ComposicaoVariavelRequest(i.CodVariavel!.Value, i.ExibirGrafico, index + 1, i.NormalidadeOverride)).ToArray())).ToArray());

    // Reverse only entities touched by an operation; edits to other fields survive.
    public static StudioDocument Reverse(StudioDocument current, StudioDocument before, StudioDocument after)
    {
        var now = Flatten(current); var a = Flatten(before); var b = Flatten(after);
        var changed = a.Keys.Union(b.Keys).Where(k => a.GetValueOrDefault(k) != b.GetValueOrDefault(k)).ToArray();
        if (changed.Any(k => now.GetValueOrDefault(k) != b.GetValueOrDefault(k))) throw new StudioException(409, "Outra edição alterou estes itens. Não é possível desfazer sem sobrescrever trabalho posterior.");
        foreach (var k in changed) { if (a.TryGetValue(k, out var value)) now[k] = value; else now.Remove(k); }
        var meta = JsonSerializer.Deserialize<StudioDocument>(now["document"], Json)!;
        var sections = now.Where(p => p.Key.StartsWith("section:")).Select(p => JsonSerializer.Deserialize<SectionEntry>(p.Value, Json)!).OrderBy(p => p.Position).ToArray();
        var items = now.Where(p => p.Key.StartsWith("item:")).Select(p => JsonSerializer.Deserialize<ItemEntry>(p.Value, Json)!).ToArray();
        if (items.Any(i => !sections.Any(s => s.Section.Id == i.SectionId))) throw new StudioException(409, "A reversão deixaria campos sem seção.");
        return meta with { Secoes = sections.Select(s => s.Section with { Itens = items.Where(i => i.SectionId == s.Section.Id).OrderBy(i => i.Position).Select(i => i.Item).ToList() }).ToList() };
    }
    private sealed record SectionEntry(StudioSection Section, int Position);
    private sealed record ItemEntry(StudioItem Item, string SectionId, int Position);
    private static Dictionary<string, string> Flatten(StudioDocument d)
    {
        var map = new Dictionary<string, string> { ["document"] = JsonSerializer.Serialize(d with { Secoes = [] }, Json) };
        for (var n = 0; n < d.Secoes.Count; n++)
        {
            var s = d.Secoes[n]; map["section:" + s.Id] = JsonSerializer.Serialize(new SectionEntry(s with { Itens = [] }, n), Json);
            for (var j = 0; j < s.Itens.Count; j++) map["item:" + s.Itens[j].Id] = JsonSerializer.Serialize(new ItemEntry(s.Itens[j], s.Id, j), Json);
        }
        return map;
    }
}
