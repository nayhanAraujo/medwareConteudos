using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MdwConteudos.Api.Modules.Avaliacoes;

public sealed class AvaliacaoInput
{
    [JsonRequired, JsonPropertyName("codversao")] public int? CodVersao { get; set; }
    [Required, StringLength(40), JsonPropertyName("origem")] public string Origem { get; set; } = "";
    [Required, StringLength(80), JsonPropertyName("cliente_id")] public string ClienteId { get; set; } = "";
    [Required, StringLength(80), JsonPropertyName("usuario_id")] public string UsuarioId { get; set; } = "";
    [Required, StringLength(150), JsonPropertyName("nome_avaliador")] public string NomeAvaliador { get; set; } = "";
    [Required, StringLength(150), JsonPropertyName("nome_clinica")] public string NomeClinica { get; set; } = "";
    [Range(1,5), JsonPropertyName("nota_geral")] public int NotaGeral { get; set; }
    [Required, RegularExpression("^(sim|talvez|nao)$"), JsonPropertyName("recomendacao")] public string Recomendacao { get; set; } = "";
    [Range(1,5), JsonPropertyName("facilidade_uso")] public int? FacilidadeUso { get; set; }
    [Range(1,5), JsonPropertyName("qualidade_conteudo")] public int? QualidadeConteudo { get; set; }
    [Range(1,5), JsonPropertyName("completude")] public int? Completude { get; set; }
    [Range(1,5), JsonPropertyName("aparencia")] public int? Aparencia { get; set; }
    [Range(1,5), JsonPropertyName("utilidade_clinica")] public int? UtilidadeClinica { get; set; }
    [StringLength(150), JsonPropertyName("titulo")] public string? Titulo { get; set; }
    [StringLength(1000), JsonPropertyName("comentario")] public string? Comentario { get; set; }
}

public sealed class AvaliacaoRecord
{
    public long Id { get; set; }
    public int CodScriptLaudo { get; set; }
    public int? CodVersao { get; set; }
    public string Origem { get; set; } = "";
    public string ClienteId { get; set; } = "";
    public string UsuarioId { get; set; } = "";
    public string NomeAvaliador { get; set; } = "";
    public string NomeClinica { get; set; } = "";
    public string NomeScript { get; set; } = "";
    public string? NumeroVersao { get; set; }
    public int NotaGeral { get; set; }
    public string Recomendacao { get; set; } = "";
    public int? FacilidadeUso { get; set; }
    public int? QualidadeConteudo { get; set; }
    public int? Completude { get; set; }
    public int? Aparencia { get; set; }
    public int? UtilidadeClinica { get; set; }
    public string? Titulo { get; set; }
    public string? Comentario { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public string Situacao { get; set; } = "nova";
    public bool Visivel { get; set; }
    public AvaliacaoPublica Publica() => new(Id, CodScriptLaudo, CodVersao, NumeroVersao, NomeAvaliador,
        NotaGeral, Recomendacao, FacilidadeUso, QualidadeConteudo, Completude, Aparencia, UtilidadeClinica,
        Titulo, Comentario, DateTime.SpecifyKind(CriadoEm, DateTimeKind.Utc), DateTime.SpecifyKind(AtualizadoEm, DateTimeKind.Utc));
}

public sealed record AvaliacaoPublica(long Id, int CodScriptLaudo, int? CodVersao, string? NumeroVersao,
    string NomeAvaliador, int NotaGeral, string Recomendacao, int? FacilidadeUso, int? QualidadeConteudo,
    int? Completude, int? Aparencia, int? UtilidadeClinica, string? Titulo, string? Comentario,
    DateTime CriadoEm, DateTime AtualizadoEm);
public sealed record AvaliacaoSave(long Id, bool Criada, bool Atualizada, AvaliacaoPublica Avaliacao);
public sealed record AvaliacaoEnvelope<T>(bool Success, T Data);
public sealed record AvaliacaoPublicPage(bool Success, IReadOnlyList<AvaliacaoPublica> Data, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record AvaliacaoEvento(long Id, string Tipo, string Autor, int? CodUsuario, DateTime DataEvento,
    string? Antes, string? Depois, string? Observacao);
public sealed record AvaliacaoPagina<T>(IReadOnlyList<T> Data, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed class AvaliacaoResumo
{
    public int Quantidade { get; set; }
    public double? MediaGeral { get; set; }
    public double? FacilidadeUso { get; set; }
    public double? QualidadeConteudo { get; set; }
    public double? Completude { get; set; }
    public double? Aparencia { get; set; }
    public double? UtilidadeClinica { get; set; }
    public Dictionary<int, int> DistribuicaoNotas { get; set; } = [];
    public Dictionary<string, int> Recomendacoes { get; set; } = [];
}
public sealed class AvaliacaoFiltro
{
    public int? Script { get; set; }
    public int? Versao { get; set; }
    public string? Sistema { get; set; }
    public int? Pacote { get; set; }
    public string? Origem { get; set; }
    public string? Clinica { get; set; }
    public DateTime? Inicio { get; set; }
    public DateTime? Fim { get; set; }
    [Range(1,5)] public int? Nota { get; set; }
    [RegularExpression("^(sim|talvez|nao)$")] public string? Recomendacao { get; set; }
    [RegularExpression("^(nova|em_analise|concluida)$")] public string? Situacao { get; set; }
    public bool? Visivel { get; set; }
    [Range(1,int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1,100)] public int PageSize { get; set; } = 25;
}
public sealed record AcompanhamentoInput([Required, RegularExpression("^(nova|em_analise|concluida)$")] string Situacao);
public sealed record AnotacaoInput([Required, StringLength(2000)] string Texto);
public sealed record VisibilidadeInput(bool Visivel, [Required, StringLength(2000)] string Justificativa);
public sealed class AvaliacaoException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
