using System.Text.Json;

namespace MdwConteudos.Api.Modules.RealtimeVoice;

public static class RealtimeTools
{
    public static readonly object[] Definitions =
    [
        Tool("consultar_rascunho", "Consulte IDs e estado atual antes de editar. Não invente IDs.", new { }),
        Tool("buscar_medidas", "Busque medidas do catálogo; refine se houver mais de uma opção. Não invente IDs.", new { busca = Str() }, "busca"),
        Tool("criar_secao", "Cria seção no rascunho. grupoOperacao identifica a intenção; para corrigir a mesma intenção use o mesmo grupo e substituirOperacao=true.",
            new { grupoOperacao = Str(), substituirOperacao = Bool(), nome = Str(), coluna = Col(), ordem = Num() }, "grupoOperacao", "substituirOperacao", "nome", "coluna", "ordem"),
        Tool("organizar_secao", "Renomeia/reordena seção existente. Preserve o grupo da operação ao corrigir.",
            new { grupoOperacao = Str(), substituirOperacao = Bool(), secaoId = Num(), nome = Str(), coluna = Col(), ordem = Num() }, "grupoOperacao", "substituirOperacao", "secaoId", "nome", "coluna", "ordem"),
        Tool("incluir_medidas", "Inclui medidas do catálogo e dependências. Se já incluída, use mover_medida. Corrigir a inclusão exige o mesmo grupo e substituirOperacao=true.",
            new { grupoOperacao = Str(), substituirOperacao = Bool(), secaoId = Num(), codVariaveis = new { type = "array", items = Num(), minItems = 1, maxItems = 30 } },
            "grupoOperacao", "substituirOperacao", "secaoId", "codVariaveis"),
        Tool("mover_medida", "Move um campo existente para outra seção/ordem sem duplicá-lo.",
            new { grupoOperacao = Str(), substituirOperacao = Bool(), campoId = Num(), secaoId = Num(), ordem = Num() }, "grupoOperacao", "substituirOperacao", "campoId", "secaoId", "ordem"),
        Tool("desfazer_alteracao", "Desfaz uma alteração reversível do agente, identificada pelo grupoOperacao.", new { grupoOperacao = Str() }, "grupoOperacao"),
        Tool("preparar_salvamento", "Propõe salvar no modelo selecionado pelo usuário. Não salva; exige confirmação pelo botão da interface.", new { nome = Str() }, "nome")
    ];
    private static object Str() => new { type = "string", minLength = 1, maxLength = 120 };
    private static object Num() => new { type = "integer", minimum = 0, maximum = 10000 };
    private static object Col() => new { type = "integer", @enum = new[] { 1, 2 } };
    private static object Bool() => new { type = "boolean" };
    private static object Tool(string name, string description, object properties, params string[] required) => new
    { type = "function", name, description, parameters = new { type = "object", properties, required, additionalProperties = false } };

    public static void Validate(string name, JsonElement args)
    {
        var tool = Definitions.Select(x => JsonSerializer.SerializeToElement(x)).FirstOrDefault(x => x.GetProperty("name").GetString() == name);
        if (tool.ValueKind == JsonValueKind.Undefined || args.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Ferramenta ou argumentos inválidos.");
        var schema = tool.GetProperty("parameters");
        foreach (var required in schema.GetProperty("required").EnumerateArray())
            if (!args.TryGetProperty(required.GetString()!, out _)) throw new InvalidOperationException("Argumento obrigatório ausente.");
        foreach (var arg in args.EnumerateObject())
        {
            if (!schema.GetProperty("properties").TryGetProperty(arg.Name, out var property)) throw new InvalidOperationException("Argumento não permitido.");
            var valid = property.GetProperty("type").GetString() switch
            {
                "string" => arg.Value.ValueKind == JsonValueKind.String && arg.Value.GetString()!.Length is > 0 and <= 120,
                "integer" => arg.Value.TryGetInt32(out var i) && i >= 0,
                "boolean" => arg.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "array" => arg.Value.ValueKind == JsonValueKind.Array && arg.Value.GetArrayLength() is > 0 and <= 30 && arg.Value.EnumerateArray().All(x => x.TryGetInt32(out var n) && n > 0),
                _ => false
            };
            if (!valid) throw new InvalidOperationException("Tipo de argumento inválido.");
        }
    }
}
