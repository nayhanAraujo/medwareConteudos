namespace MdwConteudos.Api.Modules.PaineisCadastros;

public sealed record PainelCadastroRequest(string Nome);

public sealed record PainelCadastroItem(
    int Id,
    string Nome,
    int QuantidadeVinculos,
    bool Protegido = false);

public enum PainelCadastroTipo
{
    Cliente,
    Modulo,
    PacoteComercial
}

public interface IPaineisCadastrosService
{
    Task<IReadOnlyList<PainelCadastroItem>> ListAsync(PainelCadastroTipo tipo, CancellationToken ct);
    Task CreateAsync(PainelCadastroTipo tipo, PainelCadastroRequest request, CancellationToken ct);
    Task<bool> UpdateAsync(PainelCadastroTipo tipo, int id, PainelCadastroRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(PainelCadastroTipo tipo, int id, CancellationToken ct);
}
