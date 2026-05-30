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

public record VariavelReferenciaResumoDto(int? Codigo, string? Titulo, string? Ano, string? Descricao, string? Autores);

public record VariavelNormalidadeDetalheDto(
    int CodNormalidade,
    string? Sexo,
    decimal? ValorMin,
    decimal? ValorMax,
    int? IdadeMin,
    int? IdadeMax,
    VariavelReferenciaResumoDto? Referencia);

public record VariavelEquacaoDetalheDto(
    int CodEquacao,
    string? Equacao,
    string? Linguagem,
    VariavelReferenciaResumoDto? Referencia);

public record VariavelDetalhesCompletosDto(
    IReadOnlyList<VariavelNormalidadeDetalheDto> Normalidades,
    IReadOnlyList<VariavelEquacaoDetalheDto> Equacoes);

public record VariavelCodigoDicomDto(string Codigo, string? DescricaoPtBr);

public record UnidadeMedidaDto(int CodUnidadeMedida, string Descricao);

public record VariavelCreateRequest(
    string Nome,
    string Variavel,
    string Sigla,
    string Abreviacao,
    string? Descricao,
    int CodUnidadeMedida,
    int CasasDecimais,
    IReadOnlyList<string>? Alternativas);

public record VariavelEdicaoDto(
    int CodVariavel,
    string? Nome,
    string? Variavel,
    string? Sigla,
    string? Abreviacao,
    string? Descricao,
    int? CodUnidadeMedida,
    int? CasasDecimais,
    IReadOnlyList<string> Alternativas);
