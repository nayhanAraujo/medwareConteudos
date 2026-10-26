using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace MdwConteudos.Api.Modules.Orcamentos;

public static class QuoteFileValidation
{
    public const int MaxBytes = 20 * 1024 * 1024;
    public static (string Name, string Mime) Validate(string fileName, byte[] bytes)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 200 || name.Any(char.IsControl) || bytes.Length is 0 or > MaxBytes) throw new OrcamentoException(422, "Arquivo inválido ou maior que 20 MB.");
        var ext = Path.GetExtension(name).ToLowerInvariant();
        var mime = ext switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".csv" => "text/csv", _ => throw new OrcamentoException(422, "Permitidos: PDF, DOCX, PNG, JPEG, XLSX e CSV. Bancos e backups não são aceitos.") };
        bool Starts(params byte[] signature) => bytes.AsSpan().StartsWith(signature);
        if (ext == ".pdf")
        {
            if (!Starts(37, 80, 68, 70, 45)) throw new OrcamentoException(422, "Assinatura PDF inválida.");
            var text = Encoding.Latin1.GetString(bytes);
            if (new[] { "/JavaScript", "/Launch", "/EmbeddedFile", "/Encrypt" }.Any(text.Contains)) throw new OrcamentoException(422, "PDF protegido ou com conteúdo ativo não é aceito.");
        }
        if (ext == ".png" && !Starts(137, 80, 78, 71, 13, 10, 26, 10) || (ext is ".jpg" or ".jpeg") && !Starts(255, 216, 255)) throw new OrcamentoException(422, "Assinatura de imagem inválida.");
        if (ext is ".docx" or ".xlsx")
        {
            try
            {
                using var stream = new MemoryStream(bytes, false); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                if (zip.Entries.Count > 5000 || zip.Entries.Sum(e => e.Length) > 200L * 1024 * 1024 || zip.GetEntry("[Content_Types].xml") is null
                    || zip.GetEntry(ext == ".docx" ? "word/document.xml" : "xl/workbook.xml") is null) throw new InvalidDataException();
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.Contains("..") || entry.FullName.StartsWith('/') || entry.FullName.Contains('\\') || entry.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
                    if (entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    {
                        if (entry.Length > 1024 * 1024) throw new InvalidDataException();
                        using var reader = new StreamReader(entry.Open());
                        // Parse with DTD disabled: never resolve document relationships over the network.
                        using var xml = System.Xml.XmlReader.Create(reader, new() { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
                        while (xml.Read()) if (xml.NodeType == System.Xml.XmlNodeType.Element && xml.GetAttribute("TargetMode")?.Equals("External", StringComparison.OrdinalIgnoreCase) == true) throw new InvalidDataException();
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or NotSupportedException)
            { throw new OrcamentoException(422, "Documento Office inválido, com macros, objetos incorporados ou vínculos externos."); }
        }
        if (ext == ".csv")
        {
            try { if (new UTF8Encoding(false, true).GetString(bytes).Contains('\0')) throw new DecoderFallbackException(); }
            catch (DecoderFallbackException) { throw new OrcamentoException(422, "Envie o CSV como texto UTF-8, sem conteúdo binário."); }
        }
        return (name, mime);
    }
}

public sealed partial class OrcamentoService
{
    public async Task Upload(long id, string version, string kind, IFormFile file, CancellationToken ct, bool importedEmail = false)
    {
        if (kind is not ("escopo" or "evidencia") || file.Length is <= 0 or > QuoteFileValidation.MaxBytes) throw new OrcamentoException(422, "Tipo ou tamanho de anexo inválido.");
        await using var input = file.OpenReadStream(); using var buffer = new MemoryStream();
        var chunk = new byte[65536]; int read;
        while ((read = await input.ReadAsync(chunk, ct)) > 0) { if (buffer.Length + read > QuoteFileValidation.MaxBytes) throw new OrcamentoException(422, "Arquivo maior que 20 MB."); await buffer.WriteAsync(chunk.AsMemory(0, read), ct); }
        var content = buffer.ToArray(); var info = QuoteFileValidation.Validate(file.FileName, content);
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct); var h = await ReadHead(c, id, tx, ct);
        if (importedEmail)
        {
            var source = await ReadEmailSource(c, id, tx, ct);
            if (source is null || source.ImportedBy != actor.Id || h.Revision != 1 || h.Status != "rascunho" || source.Confirmation is not null || kind != "escopo")
                throw new OrcamentoException(409, "O bot só pode anexar ao próprio rascunho antes da confirmação humana.");
            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOANEXO WHERE CODORCAMENTO=@id AND REVISAO=@revision AND TIPO='escopo' AND NOME=@name AND SHA256=@hash", new { id, revision = h.Revision, name = info.Name, hash }, tx, cancellationToken: ct)) > 0) return;
        }
        if (kind == "escopo" && !OrcamentoWorkflow.Editable(h.Status)) throw new OrcamentoException(409, "Material de escopo exige uma revisão editável.");
        await Bump(c, tx, h, version, Now, ct);
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOANEXO WHERE CODORCAMENTO=@id", new { id }, tx, cancellationToken: ct)) >= 100
            || (await c.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT SUM(TAMANHO) FROM ORCAMENTOANEXO WHERE CODORCAMENTO=@id", new { id }, tx, cancellationToken: ct)) ?? 0) + content.LongLength > 200L * 1024 * 1024)
            throw new OrcamentoException(422, "Limite por orçamento: 100 anexos e 200 MB no total, incluindo revisões anteriores.");
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTOANEXO(CODORCAMENTO,REVISAO,NOME,MIME,TAMANHO,SHA256,TIPO,CONTEUDO,CODUSUARIO,DTHRCRIACAO) VALUES(@id,@revision,@name,@mime,@size,@hash,@kind,@content,@actor,@now)",
            new { id, revision = h.Revision, name = info.Name, mime = info.Mime, size = content.LongLength, hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), kind, content, actor = actor.Id, now = Now }, tx, cancellationToken: ct));
        if (kind == "escopo") await c.ExecuteAsync(new CommandDefinition("UPDATE OR INSERT INTO ORCAMENTO_USUARIO(CODORCAMENTO,REVISAO,CODUSUARIO) VALUES(@id,@revision,@actor) MATCHING(CODORCAMENTO,REVISAO,CODUSUARIO)", new { id, revision = h.Revision, actor = actor.Id }, tx, cancellationToken: ct));
        await Event(c, tx, h, "upload", h.Status, actor, null, ct); await tx.CommitAsync(ct);
    }
    public async Task<(byte[] Bytes, string Name, string Mime)> Download(long id, long file, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); _ = await Actor(c, null, ct);
        var row = await c.QuerySingleOrDefaultAsync<FileRow>(new CommandDefinition("SELECT CONTEUDO AS Content,NOME AS Name,MIME AS Mime FROM ORCAMENTOANEXO WHERE CODORCAMENTOANEXO=@file AND CODORCAMENTO=@id", new { file, id }, cancellationToken: ct)) ?? throw new OrcamentoException(404, "Anexo não encontrado neste orçamento.");
        return (row.Content, row.Name, row.Mime);
    }
    private sealed class FileRow { public byte[] Content { get; set; } = []; public string Name { get; set; } = ""; public string Mime { get; set; } = ""; }
    public sealed record PreparedProposal(QuoteProposal Document, string Email, byte[] Pdf, bool Superseded);
    private sealed class ProposalRow { public string Document { get; set; } = ""; public string Email { get; set; } = ""; public byte[] Pdf { get; set; } = []; }
    public async Task PrepareProposal(long id, string version, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct); var actor = await Actor(c, tx, ct);
        var h = await ReadHead(c, id, tx, ct);
        if (!OrcamentoWorkflow.Approved(h.Status)) throw new OrcamentoException(409, "Somente revisões aprovadas internamente podem gerar proposta.");
        await RequireConfirmedEmailSource(c, h, tx, ct);
        if (await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ORCAMENTOPROPOSTA WHERE CODORCAMENTO=@id AND REVISAO=@revision", new { id, revision = h.Revision }, tx, cancellationToken: ct)) > 0) { await tx.CommitAsync(ct); return; }
        QuoteCalculator.Ready(h.Snapshot, DateOnly.FromDateTime(Now));
        var proposal = QuoteProposal.From(id.ToString(System.Globalization.CultureInfo.InvariantCulture), h.Revision, h.Snapshot, false);
        var pdf = proposal.Pdf();
        await Bump(c, tx, h, version, Now, ct);
        await c.ExecuteAsync(new CommandDefinition("INSERT INTO ORCAMENTOPROPOSTA(CODORCAMENTO,REVISAO,DOCUMENTO,EMAIL,PDF,SHA256,VERSAOTEMPLATE,CODUSUARIO,DTHRCRIACAO) VALUES(@id,@revision,@document,@email,@pdf,@hash,'manual-v1',@actor,@now)",
            new { id, revision = h.Revision, document = QuoteTextBlob.Json(proposal), email = new QuoteTextBlob(proposal.Email()), pdf, hash = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(), actor = actor.Id, now = Now }, tx, cancellationToken: ct));
        await Event(c, tx, h, "exportar", h.Status, actor, null, ct); await tx.CommitAsync(ct);
    }
    public async Task<PreparedProposal> Proposal(long id, int? revision, CancellationToken ct)
    {
        await using var c = await db.OpenConnectionAsync(ct); _ = await Actor(c, null, ct); var h = await ReadHead(c, id, ct: ct); var number = revision ?? h.Revision;
        var row = await c.QuerySingleOrDefaultAsync<ProposalRow>(new CommandDefinition("SELECT DOCUMENTO AS Document,EMAIL AS Email,PDF AS Pdf FROM ORCAMENTOPROPOSTA WHERE CODORCAMENTO=@id AND REVISAO=@number", new { id, number }, cancellationToken: ct))
            ?? throw new OrcamentoException(409, "Proposta ainda não preparada nesta revisão. Um usuário autorizado a registrar marcos deve prepará-la após a aprovação interna.");
        return new(OrcamentoJson.Read<QuoteProposal>(row.Document), row.Email, row.Pdf, number != h.Revision);
    }
}
