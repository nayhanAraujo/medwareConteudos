using System.Globalization;
using System.Text.Json;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed class OrcamentoException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class OrcamentoJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new OrcamentoException(500, "Registro de orçamento inválido.");
}

public sealed record QuoteHours(decimal Analysis = 0, decimal Planning = 0, decimal Development = 0, decimal Tests = 0, decimal Validation = 0)
{
    public decimal Total => Analysis + Planning + Development + Tests + Validation;
}
public sealed record QuoteItem
{
    public string? Key { get; init; }
    public QuoteTechnicalContext? TechnicalContext { get; init; }
    public List<QuoteCheckAnswer> CheckAnswers { get; init; } = [];
    public QuotePendingResolution? PendingResolution { get; init; }
    public string Category { get; init; } = "laudos";
    public string Description { get; init; } = "";
    public decimal Quantity { get; init; } = 1;
    public string Complexity { get; init; } = "simples";
    public string ComplexityReason { get; init; } = "";
    public string Reuse { get; init; } = "";
    public string Dependencies { get; init; } = "";
    public string Exclusions { get; init; } = "";
    public string Assumptions { get; init; } = "";
    public string Pending { get; init; } = "";
    public bool BlockingPending { get; init; }
    public QuoteHours Hours { get; init; } = new();
    public string Mode { get; init; } = "horas";
    public string? RuleId { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? ManualValue { get; init; }
    public string Justification { get; init; } = "";
    public decimal? DiscountPercent { get; init; }
    public decimal? DiscountValue { get; init; }
    public string DiscountReason { get; init; } = "";
}
public sealed record QuoteDraft
{
    public string Title { get; init; } = "";
    public string Request { get; init; } = "";
    public List<QuoteItem> Items { get; init; } = [];
    public DateOnly? ValidUntil { get; init; }
    public string PaymentTerms { get; init; } = "";
    public bool DeadlineValidated { get; init; }
    public string Deadline { get; init; } = "";
}
public sealed record QuoteRule(string Id, string Label, string Category, string Formula, decimal Value, bool RequiresManual = false);
public sealed record QuotePolicy
{
    public decimal? HourlyRate { get; init; }
    public decimal? InternalHourlyCost { get; init; }
    public int? ValidityDays { get; init; }
    public string PaymentTerms { get; init; } = "";
    public List<QuoteRule> Rules { get; init; } = [];
    public static QuotePolicy Initial => new()
    {
        Rules = [new("frases-equipe", "Inserção manual de frases pela equipe — POP-GO-001 v1.0", "frases", "unitario", 10),
            new("unificacao", "Unificação — enquadramento e abrangência pendentes", "unificacoes", "fixo", 300, true),
            new("remigracao", "Remigração sem alteração do migrador — confirmar enquadramento", "migracoes", "fixo", 300, true),
            new("migracao-simples", "Migração fora da implantação — análise necessária", "migracoes", "mensalidade", 500, true),
            new("migracao-complexa", "Migração complexa fora da implantação — análise necessária", "migracoes", "mensalidade", 1000, true),
            new("exportacao-medico", "Exportação segmentada — negociação e quantidade pendentes", "migracoes", "unitario", 300, true)]
    };
}
public sealed record CommercialClient(string FiscalId = "", string Contact = "", string Email = "", decimal? MonthlyFee = null);
public sealed record QuoteClient(int Id, string Name, CommercialClient Commercial);
public sealed record QuoteItemTotal(decimal Hours, decimal? Gross, decimal? Discount, decimal? Net, decimal? InternalCost, string? Pending)
{
    public List<QuoteIssue> Issues { get; init; } = [];
}
public sealed record QuoteCalculation(List<QuoteItemTotal> Items, decimal Hours, decimal? Gross, decimal? Discount, decimal? Net, decimal? InternalCost, bool Complete);
public sealed record QuoteSnapshot(QuoteDraft Draft, QuoteClient Client, QuotePolicy Policy, string PolicyVersion, QuoteCalculation Calculation);
public sealed record CreateQuoteRequest(int ClientId, QuoteDraft Draft);
public sealed record SaveQuoteRequest(string Version, QuoteDraft Draft, bool RefreshPolicy = false, bool RefreshClient = false);
public sealed record QuoteCommand(string Version, string Action, string Reason = "", string Reference = "", string Destination = "");
public sealed record SavePolicyRequest(string Version, QuotePolicy Policy, string Reason);
public sealed record SaveClientRequest(string Version, CommercialClient Commercial);
public sealed record QuoteListItem(string Id, int ClientId, string ClientName, string Title, int Revision, string Status, bool Archived, decimal? Total, string Version, string UpdatedAtUtc);
public sealed record QuoteEvent(string Id, int Revision, string Action, string FromStatus, string ToStatus, int ActorId, string ActorName, string Reason, string Reference, string Destination, string CreatedAtUtc);
public sealed record QuoteAttachment(string Id, int Revision, string Name, string Mime, long Size, string Sha256, string Kind, int ActorId, string CreatedAtUtc);
public sealed record QuoteRevision(int Number, string Status, QuoteSnapshot Snapshot, int CreatedBy, string CreatedByName, string CreatedAtUtc);
public sealed record QuoteDetail(QuoteListItem Header, QuoteSnapshot Snapshot, List<QuoteEvent> Events, List<QuoteAttachment> Attachments, List<QuoteRevision> Revisions, List<int> Editors)
{
    public List<QuoteIssue> Issues => [..QuoteIssueBuilder.For(Snapshot), ..(EmailSource is { Confirmed: false } ? new[] { new QuoteIssue("origem_email", "impedimento", null, "Confirme o cliente, o escopo e o destinatário da solicitação recebida por e-mail.", "Impede avançar a solicitação, aprovar ou enviar a proposta.", "emailSource", "Revisar origem do e-mail", 1) } : [])];
    public QuoteEmailSourceView? EmailSource { get; init; }
    public QuoteAnalysisGate AnalysisGate { get; init; } = new(false, false, false);
}
public sealed record QuoteAnalysisGate(bool Processing, bool AwaitingClassification, bool AwaitingApplication);

public static class QuoteCalculator
{
    public static readonly string[] Categories = ["laudos", "impressoes", "relatorios", "migracoes", "unificacoes", "frases", "outras"];
    public static readonly string[] Complexities = ["simples", "intermediario", "complexo", "muito_complexo"];
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    public static void CheckText(string? value, string label, int limit = 4000)
    {
        if (value is null || value.Length > limit || value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw new OrcamentoException(422, $"{label}: conteúdo inválido ou maior que {limit} caracteres.");
    }
    public static void ValidatePolicy(QuotePolicy p)
    {
        if (p is null) throw new OrcamentoException(422, "Informe a política comercial.");
        if (p.HourlyRate is < 0 or > 1000000 || p.InternalHourlyCost is < 0 or > 1000000 || p.ValidityDays is < 1 or > 3650)
            throw new OrcamentoException(422, "Preço-hora, custo ou validade inválidos.");
        CheckText(p.PaymentTerms, "Condições de pagamento");
        if (p.Rules is null || p.Rules.Count > 100 || p.Rules.Any(r => r is null) || p.Rules.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != p.Rules.Count)
            throw new OrcamentoException(422, "Catálogo de regras inválido.");
        foreach (var r in p.Rules)
        {
            if (string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 60 || r.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || !Categories.Contains(r.Category)
                || r.Formula is not ("fixo" or "unitario" or "mensalidade") || r.Value is < 0 or > 1000000)
                throw new OrcamentoException(422, "Regra comercial inválida.");
            CheckText(r.Label, "Descrição da regra", 300);
        }
    }
    public static QuoteCalculation Calculate(QuoteDraft draft, QuotePolicy policy, CommercialClient client)
    {
        if (draft is null || draft.Items is null || draft.Items.Count > 100) throw new OrcamentoException(422, "Informe até 100 itens.");
        CheckText(draft.Title, "Título", 200); CheckText(draft.Request, "Solicitação");
        CheckText(draft.PaymentTerms, "Pagamento"); CheckText(draft.Deadline, "Prazo", 500);
        ValidatePolicy(policy);
        var totals = new List<QuoteItemTotal>();
        foreach (var item in draft.Items)
        {
            if (item is null || item.Hours is null || !Categories.Contains(item.Category) || !Complexities.Contains(item.Complexity) || item.Mode is not ("horas" or "unitario" or "tabelado" or "manual"))
                throw new OrcamentoException(422, "Categoria, complexidade ou forma de cálculo inválida.");
            foreach (var text in new[] { item.Description, item.ComplexityReason, item.Reuse, item.Dependencies, item.Exclusions, item.Assumptions, item.Pending, item.Justification, item.DiscountReason }) CheckText(text, "Item");
            if (item.Quantity <= 0 || item.Quantity > 1000000 || item.UnitPrice is < 0 or > 1000000 || item.ManualValue is < 0 or > 1000000000000m
                || new[] { item.Hours.Analysis, item.Hours.Planning, item.Hours.Development, item.Hours.Tests, item.Hours.Validation }.Any(h => h < 0 || h > 100000))
                throw new OrcamentoException(422, "Quantidade, horas ou preço fora do intervalo permitido.");
            if (item.DiscountPercent.HasValue && item.DiscountValue.HasValue) throw new OrcamentoException(422, "Use desconto percentual OU em reais.");
            if (item.DiscountPercent is < 0 or > 100 || item.DiscountValue is < 0) throw new OrcamentoException(422, "Desconto inválido.");
            decimal? gross = null; var issues = new List<QuoteIssue>();
            void Block(string code, string message, string field, string action) => issues.Add(new(code, "impedimento", totals.Count, message, "Impede concluir o valor oficial e aprovar a revisão.", field, action, 3));
            if (item.Mode == "horas") { gross = policy.HourlyRate * item.Hours.Total; if (!gross.HasValue) Block("preco-hora", "Preço-hora não definido nesta revisão.", "policy", "Atualizar política"); }
            if (item.Mode == "unitario") { gross = item.UnitPrice * item.Quantity; if (!gross.HasValue) Block("preco-unitario", "Preço unitário pendente.", "unitPrice", "Informar preço unitário"); }
            if (item.Mode == "manual") { gross = item.ManualValue; if (string.IsNullOrWhiteSpace(item.Justification)) Block("justificativa-manual", "Enquadramento manual exige justificativa.", "justification", "Justificar valor manual"); if (!gross.HasValue) Block("valor-manual", "Valor manual pendente.", "manualValue", "Informar valor manual"); }
            if (item.Mode == "tabelado")
            {
                var rule = policy.Rules.SingleOrDefault(r => r.Id == item.RuleId);
                if (rule is null || rule.Category != item.Category) Block("regra-comercial", "Selecione uma regra compatível com a categoria.", "ruleId", "Selecionar regra comercial");
                else if (rule.RequiresManual) Block("regra-ambigua", "Regra ambígua: use enquadramento manual justificado e aprovação.", "mode", "Revisar enquadramento");
                else if (rule.Formula == "mensalidade" && client.MonthlyFee is null) Block("mensalidade", "Mensalidade do cliente obrigatória para esta regra.", "client", "Conferir dados comerciais");
                else gross = rule.Formula switch { "fixo" => rule.Value, "unitario" => rule.Value * item.Quantity, _ => Math.Max(rule.Value, client.MonthlyFee!.Value) };
            }
            if (item.BlockingPending) Block("pendencia-tecnica", string.IsNullOrWhiteSpace(item.Pending) ? "Confirme o escopo técnico deste item." : item.Pending, "pending", "Revisar pendência técnica");
            else if (!string.IsNullOrWhiteSpace(item.Pending) && item.PendingResolution is null) issues.Add(new("aviso-tecnico", "aviso", totals.Count, item.Pending, "Requer atenção da equipe.", "pending", "Revisar observação", 3));
            if (item.TechnicalContext is { } context)
                foreach (var rule in context.Rules)
                    foreach (var check in rule.Checks)
                    {
                        var answer = item.CheckAnswers.SingleOrDefault(a => a.Id == rule.Id + ":" + check.Id);
                        if (answer is null || answer.Status == "pendente")
                        {
                            if (check.Required) Block("checklist:" + rule.Id + ":" + check.Id, check.Label, "checklist", "Responder verificação obrigatória");
                            else issues.Add(new("checklist:" + rule.Id + ":" + check.Id, "aviso", totals.Count, check.Label, "Verificação orientativa.", "checklist", "Revisar checklist", 3));
                        }
                    }
            if (gross.HasValue) gross = Round(gross.Value);
            decimal? discount = gross.HasValue ? Round(item.DiscountValue ?? (gross.Value * (item.DiscountPercent ?? 0) / 100)) : null;
            if (discount > gross) throw new OrcamentoException(422, "Desconto não pode ultrapassar o valor do item.");
            if (discount > 0 && string.IsNullOrWhiteSpace(item.DiscountReason)) Block("justificativa-desconto", "Desconto exige justificativa para aprovação.", "discountReason", "Justificar desconto");
            var pending = issues.Any(i => i.Severity == "impedimento") ? string.Join(" ", issues.Where(i => i.Severity == "impedimento").Select(i => i.Message)) : null;
            totals.Add(new(item.Hours.Total, gross, discount, gross - discount, policy.InternalHourlyCost.HasValue ? Round(policy.InternalHourlyCost.Value * item.Hours.Total) : null, pending) { Issues = issues });
        }
        var complete = totals.Count > 0 && totals.All(t => t.Gross.HasValue && t.Pending is null);
        return new(totals, totals.Sum(t => t.Hours), complete ? totals.Sum(t => t.Gross!.Value) : null,
            complete ? totals.Sum(t => t.Discount!.Value) : null, complete ? totals.Sum(t => t.Net!.Value) : null,
            policy.InternalHourlyCost.HasValue ? totals.Sum(t => t.InternalCost ?? 0) : null, complete);
    }
    public static void Ready(QuoteSnapshot snapshot, DateOnly today)
    {
        if (!snapshot.Calculation.Complete || snapshot.Calculation.Net <= 0 || string.IsNullOrWhiteSpace(snapshot.Draft.Title)
            || snapshot.Draft.Items.Any(i => string.IsNullOrWhiteSpace(i.Description) || string.IsNullOrWhiteSpace(i.ComplexityReason)))
            throw new OrcamentoException(422, "Conclua itens, justificativas e valores positivos antes da aprovação.");
        if (snapshot.Draft.ValidUntil is null || snapshot.Draft.ValidUntil < today || string.IsNullOrWhiteSpace(snapshot.Draft.PaymentTerms))
            throw new OrcamentoException(422, "Informe validade vigente e condições de pagamento para a proposta final.");
        if (!snapshot.Draft.DeadlineValidated && !string.IsNullOrWhiteSpace(snapshot.Draft.Deadline))
            throw new OrcamentoException(422, "O prazo informado precisa estar validado ou ser removido.");
    }
    public static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
}
