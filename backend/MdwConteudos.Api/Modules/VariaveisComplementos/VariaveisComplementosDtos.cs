namespace MdwConteudos.Api.Modules.Web;

public record AlterarGrupoVariavelRequest(int? CodGrupo);
public record NomeRequest(string Nome);
public record ClassificacaoRequest(string Nome, int CodGrupo);
public record CodigosUniversaisRequest(IReadOnlyList<int> Codigos);
public record VariavelClassificacoesRequest(IReadOnlyList<int> Classificacoes);
public record EspecialidadeVinculoRequest(int CodEspecialidade, string? Descricao);
public record EspecialidadesLoteRequest(int CodEspecialidade, string? Descricao, IReadOnlyList<int> Variaveis);
public record ClassificacaoGrupoDto(int CodGrupo, string Nome);
public record ClassificacaoDto(int CodClassificacao, int CodGrupo, string Nome);
public record CodigoUniversalComplementoDto(int CodUniversal, string Codigo, string? DescricaoPtBr);
public record EspecialidadeComplementoDto(int CodEspecialidade, string Nome, string? Descricao = null);
public record AnexoComplementoDto(int CodAnexo, string? Nome, string? Descricao, string? TipoAnexo, string? Link, string? Caminho, int? CodFormula, int? CodReferencia);
public record FormulaOpcaoDto(int CodFormula, string? Formula);
public record ReferenciaOpcaoDto(int CodReferencia, string? Titulo, int? Ano);
public record EstudoVariavelDto(int CodFormula, string? Formula, string? TituloReferencia, int? AnoReferencia, int? CodAnexo, string? TipoAnexo, string? Caminho, string? Descricao);
public record ModeloModoTextoDto(int CodModelo, string Nome, int TotalSecoes);
public record SecaoModoTextoDto(int CodSecao, string Nome);
public record VariavelModoTextoDto(int CodVariavel, string? Sigla, string? Unidade, string? Formula);
public record NormalidadeModoTextoDto(string? Sexo, decimal? ValorMin, decimal? ValorMax);

public record VariavelDependenciasDto(
    int Formulas,
    int Normalidades,
    int Scripts,
    int Secoes,
    int CodigosUniversais,
    int Alternativas,
    int NomesClinicos,
    int Classificacoes,
    int Especialidades,
    int Anexos)
{
    public bool PossuiVinculos => Formulas + Normalidades + Scripts + Secoes + CodigosUniversais +
                                  Alternativas + NomesClinicos + Classificacoes + Especialidades + Anexos > 0;
}

public record ImportacaoCsVariavel(
    string Codigo,
    string Nome,
    string Sigla,
    string Abreviacao,
    string Unidade,
    bool ExisteNoBanco);
public record ImportacaoCsFormula(string Variavel, string Expressao, int CasasDecimais);
public record ImportacaoCsNormalidade(
    string Variavel,
    string Sexo,
    decimal ValorMin,
    decimal ValorMax,
    int IdadeMin,
    int IdadeMax,
    string Referencia);
public record ImportacaoCsPreview(
    IReadOnlyList<ImportacaoCsVariavel> Variaveis,
    IReadOnlyList<ImportacaoCsFormula> Formulas,
    IReadOnlyList<ImportacaoCsNormalidade> Normalidades);
public record ImportacaoCsConfirmarRequest(
    IReadOnlyList<string> VariaveisSelecionadas,
    IReadOnlyList<ImportacaoCsVariavel> Variaveis,
    IReadOnlyList<ImportacaoCsFormula> Formulas,
    IReadOnlyList<ImportacaoCsNormalidade> Normalidades);

public sealed record ImportacaoVariavelItem(
    string Codigo, string Nome, string Sigla, string Abreviacao, string Unidade,
    bool ExisteNoBanco, bool Valido, IReadOnlyList<string> Erros, int TotalFormulas, int TotalNormalidades,
    int? CodVariavelExistente = null,
    IReadOnlyList<string>? AlternativasArquivo = null,
    IReadOnlyList<ImportacaoVariavelSugestao>? Sugestoes = null);
public sealed record ImportacaoVariavelSugestao(
    int CodVariavel, string Codigo, string? Nome, string? Sigla, int Pontuacao, string Motivo);
public sealed record ImportacaoVariavelDecisao(string Codigo, string Acao, int? CodVariavelPrincipal = null);
public sealed record ImportacaoFormulaItem(string Variavel, string Expressao, int CasasDecimais, bool Valido, IReadOnlyList<string> Erros);
public sealed record ImportacaoNormalidadeItem(
    string Variavel, string Sexo, decimal? ValorMin, decimal? ValorMax, int? IdadeMin, int? IdadeMax,
    int? CodReferencia, string? Referencia, bool ReferenciaValida, bool Valido, IReadOnlyList<string> Erros,
    int? AnoReferencia = null, string? Classificacao = null, int? Pagina = null, string? Comentario = null);
public sealed record ImportacaoVariaveisPreview(
    string Formato, string Arquivo, IReadOnlyList<ImportacaoVariavelItem> Variaveis,
    IReadOnlyList<ImportacaoFormulaItem> Formulas, IReadOnlyList<ImportacaoNormalidadeItem> Normalidades,
    IReadOnlyList<string> Avisos, IReadOnlyList<string> Erros);
public sealed record ImportacaoVariaveisResultado(
    int Inseridas, int Ignoradas, int FormulasInseridas, int NormalidadesInseridas, IReadOnlyList<string> Rejeitadas,
    int AlternativasInseridas = 0, int VariaveisAtualizadas = 0, int ComentariosInseridos = 0);
