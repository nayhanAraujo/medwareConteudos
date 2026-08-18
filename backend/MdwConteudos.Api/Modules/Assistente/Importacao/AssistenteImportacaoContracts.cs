using Microsoft.AspNetCore.Http;

namespace MdwConteudos.Api.Modules.Assistente.Importacao;

public sealed class AssistenteImportacaoForm
{
    public int? CodScriptLaudoOrigem { get; set; }
    public int? CodScriptMrdOrigem { get; set; }
    public string TituloScript { get; set; } = string.Empty;
    public short TipoScript { get; set; }
    public IFormFile? ArquivoScript { get; set; }
    public string TituloMrd { get; set; } = string.Empty;
    public IFormFile? ArquivoMrd { get; set; }
    public List<int> Especialidades { get; set; } = [];
    public List<int> Procedimentos { get; set; } = [];
}

public sealed record AssistenteImportacaoResult(
    int CodScriptLaudo,
    int CodPagFotos,
    IReadOnlyCollection<int> Especialidades,
    int TotalProcedimentos);

public sealed record AssistenteArquivoPreparado(string Conteudo, string NomeArquivo, string ContentType, short? TipoScript = null);

public sealed class AssistenteImportacaoException(string message) : InvalidOperationException(message);
