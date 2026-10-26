using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace MdwConteudos.Api.Modules.Orcamentos;

public static class QuoteAiExtraction
{
    public static string Text(string name, byte[] bytes)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        string text;
        if (ext == ".csv") text = new UTF8Encoding(false, true).GetString(bytes);
        else if (ext == ".pdf")
        {
            using var pdf = PdfDocument.Open(bytes);
            if (pdf.NumberOfPages > 60) throw new OrcamentoException(422, "PDF limitado a 60 páginas por análise.");
            var output = new StringBuilder();
            foreach (var page in pdf.GetPages()) { output.AppendLine(page.Text); if (output.Length > 50_000) throw new OrcamentoException(422, "Texto do PDF excede 50 mil caracteres."); }
            text = output.ToString();
            if (string.IsNullOrWhiteSpace(text)) throw new OrcamentoException(422, "PDF sem texto selecionável. Envie as páginas relevantes como PNG/JPEG; OCR de PDF digitalizado não está habilitado.");
        }
        else if (ext is ".docx" or ".xlsx")
        {
            using var stream = new MemoryStream(bytes); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var output = new StringBuilder();
            var entries = zip.Entries.Where(e => ext == ".docx" ? e.FullName == "word/document.xml" : e.FullName == "xl/sharedStrings.xml" || e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToList();
            if (entries.Count > 60) throw new OrcamentoException(422, "Planilha limitada a 60 abas.");
            foreach (var entry in entries)
            {
                if (entry.Length > 5_000_000) throw new OrcamentoException(422, "Parte Office grande demais para análise.");
                using var xml = XmlReader.Create(entry.Open(), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5_000_000 });
                var doc = XDocument.Load(xml);
                // Preserve cell indices/shared-string references; never evaluate formulas or resolve links.
                output.AppendLine(entry.FullName);
                foreach (var el in doc.Descendants().Where(e => e.Name.LocalName is "t" or "v" or "f")) { output.Append(el.Parent?.Attribute("r")?.Value).Append(':').AppendLine(el.Value); if (output.Length > 50_000) throw new OrcamentoException(422, "Documento excede 50 mil caracteres."); }
            }
            text = output.ToString();
        }
        else throw new OrcamentoException(422, "Formato sem extração textual.");
        QuoteCalculator.CheckText(text, "Texto extraído", 50_000);
        if (string.IsNullOrWhiteSpace(text)) throw new OrcamentoException(422, "Documento sem texto útil para análise.");
        return text;
    }
}
