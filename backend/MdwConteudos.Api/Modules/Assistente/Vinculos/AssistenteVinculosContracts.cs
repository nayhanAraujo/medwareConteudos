namespace MdwConteudos.Api.Modules.Assistente.Vinculos;

public sealed record AssistenteVinculoItem(int Id, string Nome, int? Status, short? Sequencia);
public sealed record AssistenteVinculoGrupo(string Relacao, string Titulo, string DominioDestino,
    bool Ordenavel, bool SelecaoUnica, bool Obrigatorio, bool Editavel, IReadOnlyList<AssistenteVinculoItem> Itens);
public sealed record AssistenteVinculosDetalhe(string Dominio, int Id, string Nome, int? Status,
    IReadOnlyList<AssistenteVinculoGrupo> Relacoes);
public sealed record AssistenteVinculoResumoItem(int Id, string Nome, int? Status, int TotalVinculos);
public sealed record AssistenteVinculoResumo(IReadOnlyList<AssistenteVinculoResumoItem> Items,
    int Total, int Page, int PageSize);
