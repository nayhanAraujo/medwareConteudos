using System.Text.Json;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public static class StudioRealtimeAdapter
{
    private static object Tool(string name, string description, object properties, params string[] required) => new { type = "function", name, description, parameters = new { type = "object", properties, required, additionalProperties = false } };
    private static object S() => new { type = "string" };
    private static object N() => new { type = "integer" };
    public static readonly object[] Tools = [
        Tool("consultar_rascunho", "Leia o rascunho atual e sua seleção. Use os IDs UUID devolvidos, nunca invente IDs.", new { }),
        Tool("buscar_medidas", "Busque medidas. Se mais de uma opção for compatível, solicite escolha; não adivinhe.", new { busca = S() }, "busca"),
        Tool("criar_secao", "Cria seção. Posição zero é o início.", new { nome = S(), coluna = N() }, "nome", "coluna"),
        Tool("organizar_secao", "Renomeia ou move seção UUID.", new { secaoId = S(), nome = S(), coluna = N(), posicao = N() }, "secaoId", "nome", "coluna", "posicao"),
        Tool("incluir_medidas", "Inclui IDs de medidas já escolhidas na seção UUID. Para 'aqui', use a seção selecionada.", new { secaoId = S(), ids = new { type = "array", items = N() } }, "secaoId", "ids"),
        Tool("mover_medida", "Move item UUID para seção UUID.", new { itemId = S(), secaoId = S(), posicao = N() }, "itemId", "secaoId", "posicao"),
        Tool("desfazer_alteracao", "Desfaz a última operação compatível, preservando outras edições.", new { }),
        Tool("refazer_alteracao", "Refaz a última operação compatível.", new { }),
        Tool("preparar_salvamento", "Solicita revisão e salvamento na interface. Nunca confirma por voz.", new { })
    ];
    public static async Task<object> Execute(StudioDraftService drafts, string id, int owner, string name, JsonElement p, string commandId, long revision,
        MdwConteudos.Api.Modules.RealtimeVoice.RealtimeSelection? selection, CancellationToken ct, string? group = null)
    {
        if (name == "consultar_rascunho") return new { status = "completed", draft = await drafts.Get(id, owner, ct), selection };
        if (name == "buscar_medidas") return new { status = "completed", candidates = await drafts.Search(p.GetProperty("busca").GetString()!, ct), revision };
        if (name == "preparar_salvamento") return new { status = "awaiting_confirmation", message = "Use os botões de publicação da interface para conferir revisão, destino e confirmar." };
        string Section() => p.TryGetProperty("secaoId", out var s) && !string.IsNullOrWhiteSpace(s.GetString()) ? s.GetString()!
            : selection?.Revision == revision && selection.SectionId is { } selected ? selected : throw new StudioException(409, "Selecione uma seção atual ou informe o destino.");
        var (type, payload) = Map(name, p, Section);
        var updated = await drafts.Command(id, owner, new(commandId, revision, type, JsonSerializer.SerializeToElement(payload), Group: group, Source: "voz"), ct);
        return new { status = "completed", draft = updated, operation = new {type, payload} };
    }
    public static (string Type, object Payload) Map(string name, JsonElement p, Func<string>? contextualSection = null)
    {
        string Section() => contextualSection is not null ? contextualSection() : p.GetProperty("secaoId").GetString()!;
        return name switch
        {
            "criar_secao" => ("createSection", new { name = p.GetProperty("nome").GetString(), column = p.GetProperty("coluna").GetInt32() }),
            "organizar_secao" => ("editSection", new { sectionId = Section(), name = p.GetProperty("nome").GetString(), column = p.GetProperty("coluna").GetInt32(), position = p.GetProperty("posicao").GetInt32() }),
            "incluir_medidas" => ("addMeasures", new { sectionId = Section(), ids = p.GetProperty("ids") }),
            "mover_medida" => ("moveItem", new { sectionId = Section(), itemId = p.GetProperty("itemId").GetString(), position = p.GetProperty("posicao").GetInt32() }),
            "desfazer_alteracao" => ("undo", new { }),
            "refazer_alteracao" => ("redo", new { }),
            _ => throw new StudioException(400, "Ferramenta não permitida.")
        };
    }
}
