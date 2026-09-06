namespace MdwConteudos.Api.Modules.Assistente.Publicacao;

public sealed class PublicacaoOptions
{
    public bool Enabled { get; set; }
    // Stable identifier for this source database, not the server hostname.
    public string SourceKey { get; set; } = "";
}
