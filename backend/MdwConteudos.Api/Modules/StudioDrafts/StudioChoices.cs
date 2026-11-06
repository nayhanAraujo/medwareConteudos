using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace MdwConteudos.Api.Modules.StudioDrafts;

public sealed record StudioChoice(string Token, int CodVariavel, string SectionId, string CommandId);
public sealed class StudioChoices(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Studio.DraftChoice.v1");
    private sealed record Ticket(string Id, string DraftId, int Owner, long Revision, int[] Candidates, DateTime ExpiresAt);
    public string Issue(string draftId, int owner, long revision, IEnumerable<int> ids)
        => _protector.Protect(JsonSerializer.Serialize(new Ticket(Guid.NewGuid().ToString(), draftId, owner, revision, ids.Distinct().ToArray(), DateTime.UtcNow.AddMinutes(5))));
    public long Validate(string token, string draftId, int owner, int candidate)
    {
        Ticket ticket;
        try { ticket = JsonSerializer.Deserialize<Ticket>(_protector.Unprotect(token))!; }
        catch { throw new StudioException(409, "Escolha inválida ou expirada. Solicite novamente."); }
        if (ticket.DraftId != draftId || ticket.Owner != owner || ticket.ExpiresAt < DateTime.UtcNow || !ticket.Candidates.Contains(candidate))
            throw new StudioException(409, "Escolha não corresponde à pergunta atual.");
        return ticket.Revision;
    }
}
