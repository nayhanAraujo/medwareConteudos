namespace MdwConteudos.Api.Modules.FormulasModelos;

public sealed record EquacaoRequest(
    int CodLinguagem,
    int? CodReferencia,
    string Equacao,
    string? NomeFuncao);

public sealed record FormulaUpsertRequest(
    string? Nome,
    string? Descricao,
    string Formula,
    int CasasDecimais,
    IReadOnlyList<int>? VariavelIds,
    IReadOnlyList<EquacaoRequest>? Equacoes,
    int? CodVariavel = null);

public sealed record ModeloUpsertRequest(string Nome);

public sealed record SecaoVariavelRequest(
    int CodVariavel,
    bool ExibirGrafico,
    int? Ordem);

public sealed record SecaoUpsertRequest(
    string Nome,
    IReadOnlyList<SecaoVariavelRequest>? Variaveis);

public sealed record OrdenarSecoesRequest(IReadOnlyList<int>? SecaoIds);
public sealed record OrdenarVariaveisRequest(IReadOnlyList<int>? VariavelIds);

public sealed record LayoutItemRequest(
    int CodSecao,
    int X,
    int Y,
    int Largura,
    int Altura);

public sealed record SalvarLayoutRequest(IReadOnlyList<LayoutItemRequest>? Layout);

public sealed record FormulaListItem(
    int CodFormula,
    int? CodVariavel,
    string Nome,
    string Formula,
    string? Descricao,
    int CasasDecimais,
    string Variaveis,
    string Siglas);

public sealed record VariavelOpcao(
    int CodVariavel,
    string Nome,
    string Sigla,
    string? Formula,
    string? Normalidade,
    string? Unidade = null,
    string? Abreviacao = null);

public sealed record EquacaoDto(
    int CodLinguagem,
    string Linguagem,
    int? CodReferencia,
    string? Referencia,
    string Equacao,
    string? NomeFuncao);

public sealed record FormulaDetalhe(
    int CodFormula,
    int? CodVariavel,
    string Nome,
    string Formula,
    string? Descricao,
    int CasasDecimais,
    IReadOnlyList<int> VariavelIds,
    IReadOnlyList<EquacaoDto> Equacoes);

public sealed record ModeloListItem(
    int CodModelo,
    string Nome,
    int TotalSecoes);

public sealed record SecaoVariavelDto(
    int CodVariavel,
    string Nome,
    string Sigla,
    bool ExibirGrafico,
    int Ordem,
    string? Unidade = null,
    string? Normalidade = null);

public sealed record SecaoDto(
    int CodSecao,
    string Nome,
    int Ordem,
    int X,
    int Y,
    int Largura,
    int Altura,
    IReadOnlyList<SecaoVariavelDto> Variaveis);

public sealed record ModeloDetalhe(
    int CodModelo,
    string Nome,
    IReadOnlyList<SecaoDto> Secoes);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record LinguagemOpcao(int CodLinguagem, string Nome);
public sealed record ReferenciaOpcao(int CodReferencia, string Titulo, int? Ano, string? Autores);
public sealed record FormulaMeta(
    IReadOnlyList<VariavelOpcao> Variaveis,
    IReadOnlyList<LinguagemOpcao> Linguagens,
    IReadOnlyList<ReferenciaOpcao> Referencias);

public sealed record GeneratedModel(string NomeArquivo, string Conteudo, string ContentType);
