using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace MdwConteudos.Api.Modules.Automacoes;

// Reads the exported Tratados sheet, never evaluates formulas or extracts files to disk.
public static class ChatfastSpreadsheet
{
    static readonly string[] Headers = ["data", "rodada", "gestor", "itens_tratados", "observacao", "respondido_por", "hora"];
    static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static ChatfastImportRequest Read(byte[] bytes, string fileName, bool confirm)
    {
        if (bytes.Length is 0 or > 5_242_880 || !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ChatfastException(400, "Envie uma planilha XLSX de até 5 MB.");
        try
        {
            using var stream = new MemoryStream(bytes, false); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            if (zip.Entries.Count > 200 || zip.Entries.Sum(e => e.Length) > 20_971_520) throw new ChatfastException(400, "Planilha excede os limites de leitura.");
            XDocument Xml(ZipArchiveEntry entry)
            {
                using var input = entry.Open(); using var xml = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20_971_520 });
                return XDocument.Load(xml);
            }
            var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
            var workbook = zip.GetEntry("xl/workbook.xml");
            if (workbook is not null && Xml(workbook).Descendants(Ns + "workbookPr").Any(p => p.Attribute("date1904")?.Value is "1" or "true"))
                throw new ChatfastException(400, "Sistema de datas Excel 1904 não suportado. Exporte data/hora como texto em uma planilha padrão 1900.");
            var strings = sharedEntry is null ? [] : Xml(sharedEntry).Descendants(Ns + "si").Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
            string Cell(XElement c)
            {
                if (c.Element(Ns + "f") is not null) throw new ChatfastException(400, "Fórmulas não são aceitas nas células de importação.");
                var value = c.Element(Ns + "v")?.Value ?? "";
                return c.Attribute("t")?.Value switch {
                    "s" => int.TryParse(value, out var n) && n >= 0 && n < strings.Length ? strings[n] : throw new ChatfastException(400, "Texto compartilhado inválido."),
                    "inlineStr" => string.Concat(c.Descendants(Ns + "t").Select(t => t.Value)),
                    _ => value
                };
            }
            var results = new List<ChatfastImportRow>(); var matched = 0;
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
            {
                var rows = Xml(entry).Descendants(Ns + "row").ToArray();
                if (rows.Length > 5001) throw new ChatfastException(400, "Máximo de 5.000 registros por importação.");
                Dictionary<string, XElement> Cells(XElement row) => row.Elements(Ns + "c").ToDictionary(c => new string((c.Attribute("r")?.Value ?? "").TakeWhile(char.IsLetter).ToArray()), StringComparer.Ordinal);
                if (rows.Length == 0) continue;
                var header = Cells(rows[0]);
                if (!Headers.Select((h, i) => header.TryGetValue(((char)('A' + i)).ToString(), out var c) && Cell(c) == h).All(x => x)) continue;
                matched++;
                foreach (var row in rows.Skip(1))
                {
                    var cells = Cells(row); var values = Enumerable.Range(0, 7).Select(i => cells.TryGetValue(((char)('A' + i)).ToString(), out var c) ? Cell(c) : "").ToArray();
                    if (values.All(string.IsNullOrWhiteSpace)) continue;
                    foreach (var col in new[] { 0, 6 })
                        if (cells.TryGetValue(((char)('A' + col)).ToString(), out var c) && c.Attribute("t")?.Value is null or "n" && double.TryParse(values[col], NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
                            values[col] = DateTime.FromOADate(serial).ToString(col == 0 ? "dd/MM/yyyy" : "HH:mm", CultureInfo.InvariantCulture);
                    if (!int.TryParse(row.Attribute("r")?.Value, out var number)) throw new ChatfastException(400, "Linha sem número de origem.");
                    results.Add(new(number, values[0], values[1], values[2], values[3], values[4], values[5], values[6]));
                }
            }
            if (matched != 1 || results.Count == 0) throw new ChatfastException(400, "É necessária uma única aba com os sete cabeçalhos Tratados na primeira linha.");
            return new(Convert.ToHexStringLower(SHA256.HashData(bytes)), Path.GetFileName(fileName), results, confirm);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or ArgumentException or OverflowException)
        { throw new ChatfastException(400, "Arquivo XLSX inválido ou fora do formato Tratados."); }
    }
}
