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
public record ReferenciaOpcaoDto(int CodReferencia, string? Titulo, string? Ano);
public record EstudoVariavelDto(int CodFormula, string? Formula, string? TituloReferencia, string? AnoReferencia, int? CodAnexo, string? TipoAnexo, string? Caminho, string? Descricao);
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
    int Classificacoes,
    int Especialidades,
    int Anexos)
{
    public bool PossuiVinculos => Formulas + Normalidades + Scripts + Secoes + CodigosUniversais +
                                  Alternativas + Classificacoes + Especialidades + Anexos > 0;
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
