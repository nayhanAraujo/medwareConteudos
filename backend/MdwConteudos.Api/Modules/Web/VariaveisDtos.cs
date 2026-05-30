namespace MdwConteudos.Api.Modules.Web;

public record GrupoVariavelDto(int CodGrupo, string Nome);

public record VariavelScriptDto(int CodScriptLaudo, string? Nome);

public record VariavelAnexoReferenciaDto(string? Titulo, string? Ano, string? Autores);

public record VariavelAnexoDto(
    int CodAnexo,
    string? TipoAnexo,
    string? Caminho,
    string? Descricao,
    VariavelAnexoReferenciaDto? Referencia);

public record VariavelListItem(
    int CodVariavel,
    string? Nome,
    string? Variavel,
    string? Sigla,
    string? Abreviacao,
    int? CodGrupo,
    string? NomeGrupo,
    string? Formula,
    int? CasasDecimais,
    IReadOnlyList<string> Alternativas,
    IReadOnlyList<VariavelScriptDto> Scripts,
    IReadOnlyList<VariavelAnexoDto> Anexos,
    bool PossuiNormalidades);
