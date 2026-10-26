using System.Globalization;
using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace MdwConteudos.Api.Modules.Orcamentos;

// This projection is the ONLY input to public PDF/email output. Do not serialize QuoteSnapshot here.
public sealed record ProposalItem(string Description, decimal Quantity, decimal Gross, decimal Discount, decimal Net);
public sealed record QuoteProposal(string Id, int Revision, string ClientName, string Title, List<ProposalItem> Items,
    decimal Gross, decimal Discount, decimal Total, string ValidUntil, string PaymentTerms, string? Deadline, bool Superseded)
{
    public static QuoteProposal From(string id, int revision, QuoteSnapshot s, bool superseded) => new(id, revision, s.Client.Name, s.Draft.Title,
        s.Draft.Items.Select((i, n) => new ProposalItem(i.Description, i.Quantity, s.Calculation.Items[n].Gross!.Value, s.Calculation.Items[n].Discount!.Value, s.Calculation.Items[n].Net!.Value)).ToList(),
        s.Calculation.Gross!.Value, s.Calculation.Discount!.Value, s.Calculation.Net!.Value, s.Draft.ValidUntil!.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        s.Draft.PaymentTerms, s.Draft.DeadlineValidated && !string.IsNullOrWhiteSpace(s.Draft.Deadline) ? s.Draft.Deadline : null, superseded);
    private static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
    public string Email() => string.Join("\n", Lines());
    public List<string> Lines()
    {
        var lines = new List<string> { $"Assunto: Orçamento {Id} / revisão {Revision} — {Title}", "", $"Olá, {ClientName}.", "", "Após a análise da solicitação, apresentamos a proposta para os serviços abaixo:", "" };
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i]; lines.Add($"{i + 1}. {item.Description}");
            lines.Add($"Quantidade: {item.Quantity.ToString("0.####", CultureInfo.GetCultureInfo("pt-BR"))} | Referência: {Money(item.Gross)} | Desconto: {Money(item.Discount)} | Final: {Money(item.Net)}"); lines.Add("");
        }
        lines.Add($"Valor de referência: {Money(Gross)}"); lines.Add($"Descontos: {Money(Discount)}"); lines.Add($"Investimento total: {Money(Total)}");
        lines.Add($"Proposta válida até: {ValidUntil}"); lines.Add($"Condições de pagamento: {PaymentTerms}");
        if (Deadline is not null) lines.Add($"Prazo validado: {Deadline}");
        if (Superseded) lines.Add("ATENÇÃO: esta revisão foi substituída; consulte a proposta vigente antes de aprovar.");
        lines.AddRange(["", "Pedimos a confirmação do aceite para prosseguir conforme as condições desta proposta.", "", "Atenciosamente,", "Departamento de Demandas", "Medware Sistemas Médicos"]);
        return lines;
    }
    public byte[] Pdf()
    {
        // Bundled font: independent of IIS/system fonts, with no HTML or external resources.
        using var document = new PdfDocumentBuilder(1.4) { IncludeDocumentInformation = false };
        using var resource = typeof(QuoteProposal).Assembly.GetManifestResourceStream("MdwConteudos.Api.Assets.Fonts.DejaVuSans.ttf")
            ?? throw new InvalidOperationException("Fonte da proposta não encontrada no pacote da API.");
        using var buffer = new MemoryStream(); resource.CopyTo(buffer);
        var font = document.AddTrueTypeFont(buffer.ToArray());
        var measuringPage = document.AddPage(595, 842);
        var sourceLines = Lines().Select(l => l.Normalize(NormalizationForm.FormC)).ToList();
        // Check every scalar before writing: never silently replace text with missing-glyph boxes.
        foreach (var rune in string.Join("\n", sourceLines).EnumerateRunes().Distinct())
        {
            if (rune.Value is '\r' or '\n' or '\t') continue;
            try
            {
                if (!rune.IsBmp) throw new InvalidOperationException();
                measuringPage.MeasureText(rune.ToString(), 10, new PdfPoint(0, 0), font);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException or NotSupportedException)
            {
                throw new OrcamentoException(422, $"O PDF não possui glifo para o caractere '{rune}' (U+{rune.Value:X4}). O texto original foi preservado; revise esse símbolo no conteúdo público da proposta.");
            }
        }
        double Width(string text)
        {
            var letters = measuringPage.MeasureText(text, 10, new PdfPoint(0, 0), font);
            return letters.Count == 0 ? 0 : letters.Max(l => l.BoundingBox.Right) - Math.Min(0, letters.Min(l => l.BoundingBox.Left));
        }
        IEnumerable<string> Wrap(string value)
        {
            foreach (var paragraph in value.Replace("\r", "").Split('\n'))
            {
                if (paragraph.Length == 0) { yield return ""; continue; }
                var rest = paragraph.Replace('\t', ' ');
                while (rest.Length > 0 && Width(rest) > 507)
                {
                    var low = 1; var high = rest.Length;
                    while (low < high) { var mid = (low + high + 1) / 2; if (Width(rest[..mid]) <= 507) low = mid; else high = mid - 1; }
                    var split = rest.LastIndexOf(' ', low - 1, low); if (split < 1) split = low;
                    yield return rest[..split]; rest = rest[split..].TrimStart();
                }
                yield return rest;
            }
        }
        var lines = sourceLines.SelectMany(Wrap).ToList(); var pages = lines.Chunk(43).ToList();
        for (var p = 0; p < pages.Count; p++)
        {
            var page = p == 0 ? measuringPage : document.AddPage(595, 842);
            page.SetTextAndFillColor(20, 59, 110);
            page.AddText("MEDWARE | Proposta comercial", 18, new PdfPoint(44, 794), font);
            page.SetTextAndFillColor(38, 38, 38);
            var y = 760;
            foreach (var line in pages[p]) { if (line.Length > 0) page.AddText(line, 10, new PdfPoint(44, y), font); y -= 15; }
            page.AddText($"Orçamento {Id} | Revisão {Revision} | Página {p + 1}/{pages.Count}", 9, new PdfPoint(44, 45), font);
        }
        return document.Build();
    }
}
