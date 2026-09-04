namespace MdwConteudos.Api.Modules.Assistente.Dominios;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record NamedRequest(string? Nome, int Status = -1);
public sealed record EspecialidadeRequest(string? Descricao);
public sealed record GrupoRequest(string? Grupo, int? CodGrupoPai, int Status, IReadOnlyList<int>? Especialidades);
public sealed record FraseRequest(int CodGrupo, string? Codigo, string? Titulo, string? Frase, int Status);
public sealed record OperadoraRequest(string? RazaoSocial, string? NomeFantasia, string? RegistroAns, string? Cnpj);
public sealed record GrupoOperadoraRequest(string? Descricao, IReadOnlyList<int>? Operadoras);
public sealed record TabelaProcedimentoRequest(string? CodigoTuss, string? DescricaoTuss);
public sealed record ProcedimentoRequest(int? CodTabelaProcedimento, int CodEspecialidade, string? Descricao, int Status,
    IReadOnlyList<int>? Frases, IReadOnlyList<int>? Grupos, IReadOnlyList<int>? Scripts);
public sealed record ReferenciaRequest(string? Descricao, short Tipo, string? Valor, IReadOnlyList<int>? Especialidades);
public sealed record EsquemaRequest(string? Descricao, string? Imagem);
public sealed record EsquemaFotosRequest(string? Titulo, string? Esquema, IReadOnlyList<int>? Especialidades);
public sealed record ScriptRequest(string? Titulo, string? EstruturaScript, short TipoScript, int Status,
    IReadOnlyList<int>? Especialidades, IReadOnlyList<int>? Esquemas, IReadOnlyList<SequencedLink>? PaginasFotos,
    IReadOnlyList<int>? Procedimentos);
public sealed record PaginaFotosRequest(string? Titulo, string? EstruturaPagFotos, int Status,
    IReadOnlyList<int>? Especialidades, IReadOnlyList<int>? Esquemas, IReadOnlyList<SequencedLink>? Scripts);
public sealed record SequencedLink(int Id, short Sequencia);
public sealed record LinksRequest(IReadOnlyList<int>? Ids);
public sealed record SequencedLinksRequest(IReadOnlyList<SequencedLink>? Items);
public sealed record AssistenteStatusRequest(int Status);
