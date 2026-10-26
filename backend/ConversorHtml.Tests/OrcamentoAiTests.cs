using System.IO.Compression;
using System.Text;
using MdwConteudos.Api.Modules.Orcamentos;

namespace ConversorHtml.Tests;

public sealed class OrcamentoAiTests
{
    [Fact]
    public void TextBlobParametersPreserveNullAndAvoidNegativeSize()
    {
        using var command = new FirebirdSql.Data.FirebirdClient.FbCommand();
        var value = new string('ç', 5000);
        new QuoteTextBlob(value).AddParameter(command, "body");
        new QuoteTextBlob(null).AddParameter(command, "missing");
        Assert.Equal(FirebirdSql.Data.FirebirdClient.FbDbType.Text, command.Parameters["body"].FbDbType);
        Assert.Equal(value, command.Parameters["body"].Value);
        Assert.True(command.Parameters["body"].Size >= 0);
        Assert.Equal(DBNull.Value, command.Parameters["missing"].Value);
    }
    public static QuoteAiSuggestion Suggestion() => new("Escopo técnico", [new("laudos", "Novo modelo de ecocardiograma", 1, "intermediario", "Inclui medidas e validação", new(1, 1, 3, 2, 1), "Revisão médica", "Confirmar medidas", true)], ["Estimativa preliminar; validar com a equipe."]);
    [Fact]
    public void StrictOutputRejectsPricesUnknownFieldsAndInvalidEstimates()
    {
        var json = OrcamentoJson.Write(Suggestion()); Assert.Single(QuoteAiSuggestion.Parse(json).Items);
        Assert.Throws<OrcamentoException>(() => QuoteAiSuggestion.Parse(json.Replace("\"summary\":", "\"price\":1,\"summary\":")));
        Assert.Throws<OrcamentoException>(() => QuoteAiSuggestion.Parse("{}"));
        Assert.Throws<OrcamentoException>(() => QuoteAiSuggestion.Parse(OrcamentoJson.Write(Suggestion() with { Items = [Suggestion().Items[0] with { Hours = new(-1) }] })));
        var item = QuoteAiSuggestion.ToItem(Suggestion().Items[0]); Assert.Null(item.UnitPrice); Assert.Null(item.ManualValue); Assert.Equal("horas", item.Mode); Assert.True(item.BlockingPending);
    }
    [Fact]
    public void CsvAndOfficeExtractionDoNotExecuteFormulasOrLinks()
    {
        Assert.Contains("Altura", QuoteAiExtraction.Text("escopo.csv", Encoding.UTF8.GetBytes("campo;unidade\nAltura;cm")));
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) { using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()); writer.Write("<document><p><t>Medidas cardíacas</t></p></document>"); }
        Assert.Contains("Medidas cardíacas", QuoteAiExtraction.Text("escopo.docx", stream.ToArray()));
        Assert.Throws<OrcamentoException>(() => QuoteAiExtraction.Text("vazio.csv", []));
        Assert.Throws<OrcamentoException>(() => QuoteAiExtraction.Text("grande.csv", Encoding.UTF8.GetBytes(new string('x', 50_001))));
    }
    [Fact]
    public void PdfWithSelectableTextCanBeAnalyzedWithoutOcr()
    {
        var pdf = QuoteProposal.From("1", 1, OrcamentoTests.Snapshot(), false).Pdf();
        Assert.Contains("Medware", QuoteAiExtraction.Text("proposta.pdf", pdf), StringComparison.OrdinalIgnoreCase);
    }
}
