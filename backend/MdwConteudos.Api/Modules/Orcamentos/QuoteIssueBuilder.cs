namespace MdwConteudos.Api.Modules.Orcamentos;

public static class QuoteIssueBuilder
{
    public static List<QuoteIssue> For(QuoteSnapshot snapshot)
    {
        var issues = QuoteCalculator.Calculate(snapshot.Draft, snapshot.Policy, snapshot.Client.Commercial).Items.SelectMany(i => i.Issues).ToList();
        var d = snapshot.Draft;
        void Add(string code, string message, string field, string action, int step, int? index = null) => issues.Add(new(code, "impedimento", index, message, "Impede solicitar aprovação ou concluir a proposta.", field, action, step));
        if (string.IsNullOrWhiteSpace(d.Title)) Add("titulo", "Informe o título da solicitação.", "title", "Preencher título", 1);
        if (d.Items.Count == 0) Add("sem-itens", "O orçamento ainda não possui itens.", "items", "Adicionar itens", 3);
        for (var i = 0; i < d.Items.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(d.Items[i].Description)) Add("descricao", "Informe a descrição pública do serviço.", "description", "Descrever serviço", 3, i);
            if (string.IsNullOrWhiteSpace(d.Items[i].ComplexityReason)) Add("complexidade", "Justifique a complexidade estimada.", "complexityReason", "Justificar complexidade", 3, i);
        }
        if (d.ValidUntil is null || d.ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow)) Add("validade", "Informe uma validade vigente.", "validUntil", "Revisar validade", 3);
        if (string.IsNullOrWhiteSpace(d.PaymentTerms)) Add("pagamento", "Informe as condições de pagamento.", "paymentTerms", "Preencher pagamento", 3);
        if (!string.IsNullOrWhiteSpace(d.Deadline) && !d.DeadlineValidated) Add("prazo", "O prazo informado precisa de validação.", "deadline", "Validar ou remover prazo", 3);
        if (snapshot.Calculation.Complete && snapshot.Calculation.Net <= 0) Add("valor-positivo", "O valor final precisa ser positivo para aprovação.", "items", "Revisar valores", 3);
        return issues;
    }
}
