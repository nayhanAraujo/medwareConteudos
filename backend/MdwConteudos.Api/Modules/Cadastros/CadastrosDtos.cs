namespace MdwConteudos.Api.Modules.Cadastros;

public sealed record NomeRequest(string Nome);
public sealed record UnidadeRequest(string Descricao, int Status);
public sealed record EspecialidadeRequest(string Nome);
public sealed record CodigoUniversalRequest(string TipoCodigo, string Codigo, string? Descricao, string? Unidade, string? DescricaoPtBr);
public sealed record AutorRequest(string Nome, string? Abreviacao, string? Orcid, string? Pais, string? Url, int? CodTipo);
public sealed record TipoAutorRequest(string Descricao);
public sealed record PacoteRequest(string Nome, string? Descricao, IReadOnlyList<int>? CodigosScripts);
public sealed record GrupoVariavelRequest(string Nome, string? Descricao, int Ativo);
public sealed record AssociarGrupoRequest(IReadOnlyList<int> CodigosVariaveis, int? CodGrupo);
public sealed record GrupoClassificacaoRequest(string Nome);
public sealed record ClassificacaoRequest(string Nome, int CodGrupo);

public sealed record UnidadeDto(int CodUnidadeMedida, string Descricao, int Status);
public sealed record LinguagemDto(int CodLinguagem, string Nome);
public sealed record EspecialidadeDto(int CodEspecialidade, string Nome);
public sealed record CodigoUniversalDto(int CodUniversal, string TipoCodigo, string Codigo, string? Descricao, string? Unidade, string? DescricaoPtBr);
public sealed record TipoAutorDto(int CodTipo, string Descricao);
public sealed record AutorDto(int CodAutor, string Nome, string? Abreviacao, string? Orcid, string? Pais, string? Url, int? CodTipo, string? Tipo);
public sealed record PacoteDto(int CodPacote, string Nome, string? Descricao, DateTime? DataAlteracao, int QuantidadeScripts);
public sealed record ScriptPacoteDto(int CodScriptLaudo, string Nome, int? CodPacote);
public sealed record GrupoVariavelDto(int CodGrupo, string Nome, string? Descricao, int Ativo, int QuantidadeVariaveis);
public sealed record VariavelGrupoDto(int CodVariavel, string Nome, string Variavel, int? CodGrupo);
public sealed record ClassificacaoDto(int CodClassificacao, string Nome, int CodGrupo);
public sealed record GrupoClassificacaoDto(int CodGrupo, string Nome, int QuantidadeClassificacoes);
