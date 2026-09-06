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

public sealed class VersaoPainelResumoRow
{
    public int CodVersaoPainel { get; set; }
    public string NumeroVersao { get; set; } = string.Empty;
    public DateTime? DataCriacao { get; set; }
    public int Publicado { get; set; }
    public string? Observacoes { get; set; }
    public int TotalImagens { get; set; }
    public int TotalArquivos { get; set; }
}

public sealed class VersaoPainelDetalheRow
{
    public int CodVersaoPainel { get; set; }
    public int CodPainel { get; set; }
    public string NumeroVersao { get; set; } = string.Empty;
    public DateTime? DataCriacao { get; set; }
    public int Publicado { get; set; }
    public string? Observacoes { get; set; }
    public string NomePainel { get; set; } = string.Empty;
    public string TipoPainel { get; set; } = string.Empty;
}

public sealed class ConfiguracaoPowerBiRow
{
    public int CodConfiguracao { get; set; }
    public string? DiretorioPbix { get; set; }
    public string? NomeArquivoPbix { get; set; }
    public string? WorkspacePowerbi { get; set; }
    public string? DatasetPowerbi { get; set; }
    public string? GatewayPowerbi { get; set; }
    public string? FrequenciaAtualizacao { get; set; }
    public string? ResponsavelAtualizacao { get; set; }
    public string? PublicLink { get; set; }
    public string? IframeCode { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class ConfiguracaoApiRow
{
    public int CodConfiguracao { get; set; }
    public string? EnderecoApi { get; set; }
    public string? ResponsavelApi { get; set; }
    public string? Observacoes { get; set; }
    public int TemJson { get; set; }
}

public sealed class ImagemPainelRow
{
    public int CodImagemPainel { get; set; }
    public string? NomeArquivo { get; set; }
    public int? TamanhoArquivo { get; set; }
    public string? TipoImagem { get; set; }
    public int? OrdemExibicao { get; set; }
}

public sealed class ArquivoVersaoRow
{
    public int CodArquivoVersao { get; set; }
    public string? TipoArquivo { get; set; }
    public string? NomeArquivo { get; set; }
    public int? TamanhoArquivo { get; set; }
    public string? MimeType { get; set; }
    public DateTime? DataCriacao { get; set; }
}

public sealed class MetricaPainelRow
{
    public int CodMetrica { get; set; }
    public string? Nome { get; set; }
    public string? UnidadeMedida { get; set; }
    public string? Conceito { get; set; }
    public string? MedidaDax { get; set; }
}

public sealed class DimensaoPainelRow
{
    public int CodDimensao { get; set; }
    public string? Nome { get; set; }
    public string? Descricao { get; set; }
    public string? DominioHierarquia { get; set; }
}

public sealed class FonteDadosPainelRow
{
    public int CodFonteDados { get; set; }
    public string? Origem { get; set; }
    public string? MetodoExtracao { get; set; }
    public string? Arquivo { get; set; }
    public string? PeriodoAtualizacao { get; set; }
    public string? Responsavel { get; set; }
}
