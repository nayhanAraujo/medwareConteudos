namespace MdwConteudos.Api.Modules.Web;

public record PainelStatusRequest(object Ativo);

public record PainelUpsertForm(
    string Nome,
    int CodModulo,
    string TipoPainel,
    string? Descricao,
    int? CodCliente,
    int Ativo,
    string? ResponsavelApi,
    string? DiretorioPbix,
    string? NomeArquivoPbixMeta,
    string? WorkspacePowerbi,
    string? DatasetPowerbi,
    string? GatewayPowerbi,
    string? FrequenciaAtualizacao,
    string? ResponsavelAtualizacao,
    List<int>? Pacotes
);
