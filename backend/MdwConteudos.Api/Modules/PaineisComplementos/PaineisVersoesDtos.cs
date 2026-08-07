namespace MdwConteudos.Api.Modules.PaineisComplementos;

public sealed class VersaoPainelForm
{
    public string NumeroVersao { get; set; } = string.Empty;
    public bool Publicado { get; set; }
    public string? Observacoes { get; set; }
    public string? DiretorioPbix { get; set; }
    public string? NomeArquivoPbix { get; set; }
    public string? WorkspacePowerbi { get; set; }
    public string? DatasetPowerbi { get; set; }
    public string? GatewayPowerbi { get; set; }
    public string? FrequenciaAtualizacao { get; set; }
    public string? ResponsavelAtualizacao { get; set; }
    public string? PublicLink { get; set; }
    public string? IframeCode { get; set; }
    public string? EnderecoApi { get; set; }
    public string? ResponsavelApi { get; set; }
    public IFormFile? JsonApi { get; set; }
    public IFormFile? ManualDocx { get; set; }
    public IFormFile? InsightsDocx { get; set; }
    public List<IFormFile> Imagens { get; set; } = [];
}

public sealed record MetricaRequest(string Nome, string? UnidadeMedida, string? Conceito, string? MedidaDax);
public sealed record DimensaoRequest(string Nome, string? Descricao, string? DominioHierarquia);
public sealed record FonteDadosRequest(string Origem, string? MetodoExtracao, string? Arquivo, string? PeriodoAtualizacao, string? Responsavel);

public sealed record ArquivoVersao(byte[] Bytes, string FileName, string ContentType);
