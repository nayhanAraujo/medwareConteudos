using System.IO.Compression;
using System.Text;
using MdwConteudos.Api.Modules.Orcamentos;

namespace ConversorHtml.Tests;

public sealed class OrcamentoTests
{
    public static QuoteItem Item(string category = "laudos", decimal value = 100) => new() { Category = category, Description = "Serviço público", ComplexityReason = "Análise validada", Mode = "manual", ManualValue = value, Justification = "Enquadramento confirmado" };
    public static QuoteDraft Draft(params QuoteItem[] items) => new() { Title = "Proposta de teste", Items = items.ToList(), PaymentTerms = "Condição autorizada pela gestão", ValidUntil = new DateOnly(2099, 12, 31) };
    public static QuoteSnapshot Snapshot(QuoteDraft? draft = null, QuotePolicy? policy = null, CommercialClient? commercial = null)
    {
        draft ??= Draft(Item()); policy ??= QuotePolicy.Initial; commercial ??= new();
        return new(draft, new(1, "Clínica Exemplo", commercial), policy, "1", QuoteCalculator.Calculate(draft, policy, commercial));
    }

    [Fact]
    public void MixedCategoriesAndHoursDoNotMultiplyTheSameWorkTwice()
    {
        var hourly = Item() with { Mode = "horas", Quantity = 33, Hours = new(1, 2, 3, 4, 5) };
        var unit = Item("impressoes") with { Mode = "unitario", UnitPrice = 10, Quantity = 33 };
        var total = Snapshot(Draft(hourly, unit), QuotePolicy.Initial with { HourlyRate = 100, InternalHourlyCost = 25 }).Calculation;
        Assert.True(total.Complete); Assert.Equal(15, total.Hours); Assert.Equal(1830, total.Net); Assert.Equal(375, total.InternalCost);
    }
    [Fact]
    public void UnknownRatesAndAmbiguousRulesNeverBecomeAValidZeroPrice()
    {
        var hour = Snapshot(Draft(Item() with { Mode = "horas", Hours = new(1) })).Calculation;
        Assert.False(hour.Complete); Assert.Null(hour.Net);
        foreach (var rule in QuotePolicy.Initial.Rules.Where(r => r.RequiresManual))
        {
            var c = Snapshot(Draft(Item(rule.Category) with { Mode = "tabelado", RuleId = rule.Id, Quantity = 5 })).Calculation;
            Assert.False(c.Complete); Assert.Null(c.Net); Assert.Contains("ambígua", c.Items[0].Pending);
        }
        var phrases = Snapshot(Draft(Item("frases") with { Mode = "tabelado", RuleId = "frases-equipe", Quantity = 3 })).Calculation;
        Assert.True(phrases.Complete); Assert.Equal(30, phrases.Net);
    }
    [Fact]
    public void MonthlyRuleRequiresSnapshotAndUsesMaximumNotAddition()
    {
        var p = QuotePolicy.Initial with { Rules = [new("migration-approved", "Regra aprovada pela gestão", "migracoes", "mensalidade", 500)] };
        var d = Draft(Item("migracoes") with { Mode = "tabelado", RuleId = "migration-approved" });
        Assert.False(Snapshot(d, p).Calculation.Complete);
        Assert.Equal(800, Snapshot(d, p, new(MonthlyFee: 800)).Calculation.Net);
        Assert.Equal(500, Snapshot(d, p, new(MonthlyFee: 200)).Calculation.Net);
    }
    [Fact]
    public void DiscountsHaveAnExplicitExclusiveModeAndRounding()
    {
        var item = Item(value: 10.05m) with { DiscountPercent = 10, DiscountReason = "Aprovado pela gestão" };
        var total = Snapshot(Draft(item)).Calculation;
        Assert.Equal(1.01m, total.Discount); Assert.Equal(9.04m, total.Net);
        Assert.Throws<OrcamentoException>(() => Snapshot(Draft(item with { DiscountValue = 1 })));
        Assert.Throws<OrcamentoException>(() => Snapshot(Draft(item with { DiscountPercent = null, DiscountValue = 100 })));
        Assert.False(Snapshot(Draft(item with { DiscountReason = "" })).Calculation.Complete);
        Assert.Throws<OrcamentoException>(() => Snapshot(Draft(item with { Quantity = -1 })));
    }
    [Fact]
    public void WorkflowCannotSkipApprovalPaymentOrIndependentApprover()
    {
        var s = Snapshot(); var date = new DateOnly(2026, 10, 23);
        Assert.Equal("em_aprovacao", OrcamentoWorkflow.Next("rascunho", new("1", "solicitar_aprovacao"), true, s, date));
        Assert.Equal(403, Assert.Throws<OrcamentoException>(() => OrcamentoWorkflow.Next("em_aprovacao", new("1", "aprovar"), true, s, date)).Status);
        Assert.Equal("aprovado", OrcamentoWorkflow.Next("em_aprovacao", new("1", "aprovar"), false, s, date));
        Assert.Throws<OrcamentoException>(() => OrcamentoWorkflow.Next("enviado", new("1", "confirmar_pagamento", Reference: "recibo"), false, s, date));
        Assert.Throws<OrcamentoException>(() => OrcamentoWorkflow.Next("aceito", new("1", "confirmar_pagamento"), false, s, date));
        Assert.Throws<OrcamentoException>(() => OrcamentoWorkflow.Next("pago", new("1", "encaminhar"), false, s, date));
        Assert.Equal("encaminhado", OrcamentoWorkflow.Next("pago", new("1", "encaminhar", Destination: "Equipe de demandas"), false, s, date));
        Assert.Throws<OrcamentoException>(() => OrcamentoWorkflow.Next("em_aprovacao", new("1", "devolver"), false, s, date));
        Assert.False(OrcamentoWorkflow.Editable("aprovado"));
    }
    [Fact]
    public void FinalEmissionRequiresRealConditionsAndValidatedDeadline()
    {
        var today = new DateOnly(2026, 10, 23);
        Assert.Throws<OrcamentoException>(() => QuoteCalculator.Ready(Snapshot(Draft(Item()) with { ValidUntil = null }), today));
        Assert.Throws<OrcamentoException>(() => QuoteCalculator.Ready(Snapshot(Draft(Item()) with { PaymentTerms = "" }), today));
        Assert.Throws<OrcamentoException>(() => QuoteCalculator.Ready(Snapshot(Draft(Item()) with { Deadline = "30 dias" }), today));
        Assert.Throws<OrcamentoException>(() => QuoteCalculator.Ready(Snapshot(Draft(Item()) with { ValidUntil = today.AddDays(-1) }), today));
        Assert.Throws<OrcamentoException>(() => QuoteCalculator.Ready(Snapshot(Draft(Item(value: 0))), today));
    }
    [Fact]
    public void PublicProjectionExcludesInternalTextCostAndUnvalidatedDeadline()
    {
        var s = Snapshot(Draft(Item() with { Assumptions = "INTERNAL_ASSUMPTION", Dependencies = "INTERNAL_DEPENDENCY", Reuse = "INTERNAL_REUSE", Pending = "INTERNAL_PENDING", Exclusions = "INTERNAL_EXCLUSION" }) with { Request = "INTERNAL_REQUEST", Deadline = "UNVALIDATED_DEADLINE" }, QuotePolicy.Initial with { InternalHourlyCost = 12345 });
        var proposal = QuoteProposal.From("9223372036854775807", 1, s, false);
        var email = proposal.Email(); var pdf = proposal.Pdf(); var text = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text); Assert.Contains("xref", text); Assert.Contains("startxref", text);
        using var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var publicText = string.Join("\n", parsed.GetPages().Select(p => p.Text));
        foreach (var secret in new[] { "INTERNAL_", "UNVALIDATED_DEADLINE", "12345" }) { Assert.DoesNotContain(secret, email); Assert.DoesNotContain(secret, text); Assert.DoesNotContain(secret, publicText); }
        using var repeated = UglyToad.PdfPig.PdfDocument.Open(proposal.Pdf());
        // The PDF library assigns fresh document IDs; public content remains identical.
        Assert.Equal(publicText, string.Join("\n", repeated.GetPages().Select(p => p.Text)));
        Assert.Contains("100,00", email);
    }
    [Fact]
    public void ProposalPdfPreservesTechnicalSymbolsAndWrapsLongText()
    {
        const string text = "Medição ≥ 10 e ≤ 20 → área em cm²; α ± β ≠ Ω — impressão";
        var proposal = QuoteProposal.From("17", 2, Snapshot(Draft(Item() with { Description = text + "\n" + new string('W', 180) })), false);
        var bytes = proposal.Pdf();
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var extracted = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        static string Compact(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
        Assert.Contains(Compact(text), Compact(extracted));
        Assert.Contains("100,00", extracted);
        Assert.All(pdf.GetPages().SelectMany(p => p.Letters), l => Assert.InRange(l.BoundingBox.Right, 0, 552));
        Assert.Equal(text + "\n" + new string('W', 180), proposal.Items[0].Description);
    }
    [Fact]
    public void ProposalPdfRejectsMissingGlyphWithoutChangingPublicText()
    {
        var proposal = QuoteProposal.From("18", 1, Snapshot(Draft(Item() with { Description = "Teste \U0001F9EC" })), false);
        var error = Assert.Throws<OrcamentoException>(() => proposal.Pdf());
        Assert.Equal(422, error.Status); Assert.Contains("U+1F9EC", error.Message);
        Assert.Contains("\U0001F9EC", proposal.Email());
    }
    [Fact]
    public void ProposalPdfRejectsUnassignedBmpCharacter()
    {
        var proposal = QuoteProposal.From("18", 1, Snapshot(Draft(Item() with { Description = "Teste \u0378" })), false);
        Assert.Contains("U+0378", Assert.Throws<OrcamentoException>(() => proposal.Pdf()).Message);
    }
    [Fact]
    public void AttachmentValidationRejectsBackupsFakeTypesActiveDocumentsAndExternalOfficeLinks()
    {
        Assert.Throws<OrcamentoException>(() => QuoteFileValidation.Validate("client.fdb", [1, 2, 3]));
        Assert.Throws<OrcamentoException>(() => QuoteFileValidation.Validate("test.png", [1, 2, 3]));
        Assert.Throws<OrcamentoException>(() => QuoteFileValidation.Validate("test.pdf", Encoding.ASCII.GetBytes("%PDF-1.4 /JavaScript")));
        Assert.Throws<OrcamentoException>(() => QuoteFileValidation.Validate("test.csv", [255, 0]));
        Assert.Equal("text/csv", QuoteFileValidation.Validate("test.csv", Encoding.UTF8.GetBytes("Nome;Quantidade\nModelo;2")).Mime);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            zip.CreateEntry("[Content_Types].xml"); zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(zip.CreateEntry("word/_rels/document.xml.rels").Open()); writer.Write("<Relationships><Relationship TargetMode='External' Target='https://external.invalid'/></Relationships>");
        }
        Assert.Throws<OrcamentoException>(() => QuoteFileValidation.Validate("test.docx", stream.ToArray()));
    }
}
