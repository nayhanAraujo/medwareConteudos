namespace MdwConteudos.Api.Modules.ScriptsComparacao;

public sealed record ScriptComparacaoResumo(
    int CodScriptLaudo,
    string Nome,
    string? Descricao,
    string? Sistema,
    string? Linguagem);

public sealed record ScriptVersaoComparacaoItem(
    int CodVersao,
    string NumeroVersao,
    string? DescricaoAlteracoes,
    string? AlteracoesInterface,
    string? AlteracoesCodigo,
    DateTime? DataCriacao,
    string? UsuarioResponsavel,
    bool Ativo,
    bool Aprovado,
    string? AprovadoPor);

public sealed record ScriptsComparacaoCatalogo(
    ScriptComparacaoResumo Script,
    IReadOnlyList<ScriptVersaoComparacaoItem> Versoes);

public sealed record ScriptsComparacaoResultado(
    ScriptComparacaoResumo Script,
    ScriptVersaoComparacaoItem Versao1,
    ScriptVersaoComparacaoItem Versao2,
    int? DiasEntreVersoes,
    bool MesmoResponsavel,
    bool MesmoStatusAprovacao);
