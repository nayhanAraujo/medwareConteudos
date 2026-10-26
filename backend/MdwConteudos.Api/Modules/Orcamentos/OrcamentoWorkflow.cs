namespace MdwConteudos.Api.Modules.Orcamentos;

public static class OrcamentoWorkflow
{
    public static bool Editable(string status) => status is "rascunho" or "analise" or "devolvido";
    public static bool Approved(string status) => status is "aprovado" or "enviado" or "aceito" or "pago" or "encaminhado";
    public static string Permission(string action) => action switch
    {
        "aprovar" or "devolver" => "aprovar",
        "enviar" or "aceitar" or "confirmar_pagamento" or "encaminhar" or "recusar" => "registrar_marcos",
        "analise" or "solicitar_aprovacao" or "nova_revisao" or "cancelar" or "arquivar" => "elaborar",
        _ => throw new OrcamentoException(422, "Ação de orçamento desconhecida.")
    };
    public static string Next(string status, QuoteCommand command, bool isEditor, QuoteSnapshot snapshot, DateOnly today)
    {
        _ = Permission(command.Action);
        QuoteCalculator.CheckText(command.Reason, "Motivo", 2000);
        QuoteCalculator.CheckText(command.Reference, "Referência", 500);
        QuoteCalculator.CheckText(command.Destination, "Destino", 500);
        if (command.Action is "devolver" or "cancelar" or "recusar" or "nova_revisao" or "arquivar" && string.IsNullOrWhiteSpace(command.Reason))
            throw new OrcamentoException(422, "Informe o motivo da decisão.");
        if (command.Action == "nova_revisao") return "rascunho";
        if (command.Action == "arquivar")
        {
            if (status is not ("cancelado" or "recusado" or "encaminhado")) throw new OrcamentoException(409, "Arquive somente orçamentos encerrados.");
            return status;
        }
        if (command.Action == "cancelar")
        {
            if (status is "cancelado" or "recusado" or "encaminhado") throw new OrcamentoException(409, "Orçamento já encerrado.");
            return "cancelado";
        }
        var next = (status, command.Action) switch
        {
            ("rascunho" or "devolvido", "analise") => "analise",
            ("rascunho" or "analise" or "devolvido", "solicitar_aprovacao") => "em_aprovacao",
            ("em_aprovacao", "aprovar") => "aprovado",
            ("em_aprovacao", "devolver") => "devolvido",
            ("aprovado", "enviar") => "enviado",
            ("enviado", "aceitar") => "aceito",
            ("enviado", "recusar") => "recusado",
            ("aceito", "confirmar_pagamento") => "pago",
            ("pago", "encaminhar") => "encaminhado",
            _ => throw new OrcamentoException(409, "Transição não permitida no estado atual.")
        };
        if (command.Action == "aprovar" && isEditor) throw new OrcamentoException(403, "O elaborador e os editores comerciais não podem aprovar a própria revisão, inclusive administradores.");
        if (command.Action is "solicitar_aprovacao" or "aprovar" or "enviar") QuoteCalculator.Ready(snapshot, today);
        if (command.Action == "confirmar_pagamento" && string.IsNullOrWhiteSpace(command.Reference)) throw new OrcamentoException(422, "Informe a referência da confirmação de pagamento.");
        if (command.Action == "encaminhar" && string.IsNullOrWhiteSpace(command.Destination)) throw new OrcamentoException(422, "Informe o destino da execução.");
        return next;
    }
}
