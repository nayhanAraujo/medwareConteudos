namespace MdwConteudos.Api.Modules.Orcamentos;

public static class QuoteTechnicalEditing
{
    public static QuoteDraft Prepare(QuoteDraft draft, QuoteDraft? previous, int actor, DateTime now)
    {
        if (draft?.Items is null) throw new OrcamentoException(422, "Informe os itens.");
        var keys = new HashSet<string>(); var result = new List<QuoteItem>();
        foreach (var item in draft.Items)
        {
            if (item is null) throw new OrcamentoException(422, "Item inválido.");
            var old = item.Key is null ? previous?.Items.ElementAtOrDefault(result.Count) is { Key: null } legacy ? legacy : null : previous?.Items.SingleOrDefault(p => p.Key == item.Key);
            var key = item.Key ?? Guid.NewGuid().ToString("N");
            if (!Guid.TryParse(key, out _) || !keys.Add(key)) throw new OrcamentoException(422, "Identificador de item inválido ou duplicado.");
            if (OrcamentoJson.Write(item.TechnicalContext) != OrcamentoJson.Write(old?.TechnicalContext)) throw new OrcamentoException(422, "O contexto técnico é definido pela aplicação da análise e não pode ser alterado diretamente.");
            QuotePendingResolution? resolution = null;
            if (!item.BlockingPending && (old?.BlockingPending == true || old?.PendingResolution is not null || !string.IsNullOrWhiteSpace(old?.Pending) && (item.PendingResolution is not null || string.IsNullOrWhiteSpace(item.Pending))))
            {
                QuoteCalculator.CheckText(item.PendingResolution?.Reason, "Justificativa da resolução", 2000);
                if (string.IsNullOrWhiteSpace(item.PendingResolution!.Reason)) throw new OrcamentoException(422, "Justifique a resolução da pendência técnica.");
                resolution = old?.PendingResolution is { } prior && prior.Reason == item.PendingResolution.Reason && old.Pending == item.Pending ? prior : new(item.PendingResolution.Reason, actor, QuoteCalculator.Utc(now));
            }
            var answers = new List<QuoteCheckAnswer>();
            var scopeChanged = old is not null && (old.Category != item.Category || old.Description != item.Description || old.Dependencies != item.Dependencies || old.Reuse != item.Reuse || old.Exclusions != item.Exclusions || old.Assumptions != item.Assumptions || old.Pending != item.Pending);
            if (item.CheckAnswers is null || item.CheckAnswers.Any(a => a is null) || item.CheckAnswers.Select(a => a.Id).Distinct().Count() != item.CheckAnswers.Count) throw new OrcamentoException(422, "Respostas do checklist inválidas.");
            var allowed = old?.TechnicalContext?.Rules.SelectMany(r => r.Checks.Select(c => r.Id + ":" + c.Id)).ToHashSet() ?? [];
            foreach (var answer in item.CheckAnswers)
            {
                if (!allowed.Contains(answer.Id) || answer.Status is not ("pendente" or "confirmado" or "nao_aplicavel")) throw new OrcamentoException(422, "Verificação ou resposta desconhecida.");
                QuoteCalculator.CheckText(answer.Reason, "Justificativa do checklist", 2000);
                if (answer.Status != "pendente" && string.IsNullOrWhiteSpace(answer.Reason)) throw new OrcamentoException(422, "Justifique a confirmação ou não aplicabilidade da verificação.");
                var prior = old!.CheckAnswers.SingleOrDefault(a => a.Id == answer.Id);
                var unchanged = prior is not null && prior.Status == answer.Status && prior.Reason == answer.Reason;
                answers.Add(answer.Status == "pendente" || scopeChanged && unchanged ? answer with { Status = "pendente", ActorId = null, ResolvedAtUtc = null } : unchanged ? prior! : answer with { ActorId = actor, ResolvedAtUtc = QuoteCalculator.Utc(now) });
            }
            result.Add(item with { Key = key, CheckAnswers = answers, PendingResolution = resolution, Pending = resolution is not null && string.IsNullOrWhiteSpace(item.Pending) ? old!.Pending : item.Pending });
        }
        return draft with { Items = result };
    }
}
