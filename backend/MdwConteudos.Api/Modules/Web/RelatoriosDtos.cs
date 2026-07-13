using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Web;

public record RelatorioUpsertRequest(
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("modulo")] string? Modulo,
    [property: JsonPropertyName("formato")] string? Formato,
    [property: JsonPropertyName("conteudo")] string? Conteudo,
    [property: JsonPropertyName("ativo")] object? Ativo = null
);

public record RelatorioStatusRequest(
    [property: JsonPropertyName("ativo")] object Ativo
);

public record RelatorioExportRequest(
    [property: JsonPropertyName("codRelatorios")]
    List<int>? CodRelatorios
);

public record ModuloRelatorioCreateRequest(
    [property: JsonPropertyName("nomeModulo")] string? NomeModulo,
    [property: JsonPropertyName("descricao")] string? Descricao,
    [property: JsonPropertyName("sistemas")] List<int>? Sistemas
);

public record ModuloRelatorioUpdateRequest(
    [property: JsonPropertyName("novoNome")] string? NovoNome,
    [property: JsonPropertyName("descricao")] string? Descricao,
    [property: JsonPropertyName("sistemas")] List<int>? Sistemas
);
