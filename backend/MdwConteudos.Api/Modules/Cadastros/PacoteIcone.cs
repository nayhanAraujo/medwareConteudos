using System.Buffers.Binary;
using System.Data;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.Cadastros;

public static class PacoteIcone
{
    public const string Prefix = "data:image/png;base64,";
    public const int MaxBytes = 1024 * 1024;

    // Firebird requires an explicit text BLOB; Dapper uses Size=-1 for long strings.
    public sealed class TextBlob(string? value) : SqlMapper.ICustomQueryParameter
    {
        public void AddParameter(IDbCommand command, string name) =>
            command.Parameters.Add(new FbParameter(name, FbDbType.Text) { Value = (object?)value ?? DBNull.Value });
    }

    public static string? Validate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal) || value.Length > Prefix.Length + 4 * ((MaxBytes + 2) / 3))
            throw new InvalidOperationException("O icone deve ser uma imagem PNG em base64 de ate 1 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value[Prefix.Length..]); }
        catch (FormatException) { throw new InvalidOperationException("Base64 do icone invalido."); }
        if (bytes.Length > MaxBytes || bytes.Length < 45 ||
            !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13 ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) != 128 ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) != 128 ||
            !bytes.AsSpan(bytes.Length - 12, 12).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }))
            throw new InvalidOperationException("O icone deve ser uma imagem PNG de 128 x 128 pixels e ate 1 MB.");
        return Prefix + Convert.ToBase64String(bytes);
    }

    public static async Task MigrateAsync(IFirebirdConnectionFactory db, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME='PACOTES' AND RDB$FIELD_NAME='ICONE_BASE64'", cancellationToken: ct));
        if (exists == 0)
            await connection.ExecuteAsync(new CommandDefinition("ALTER TABLE PACOTES ADD ICONE_BASE64 BLOB SUB_TYPE TEXT CHARACTER SET UTF8", cancellationToken: ct));

        var icons = new (string Name, string File)[]
        {
            ("Cardiologia", "cardiologia"), ("Ultrassonografia", "ultrassonografia"),
            ("Angiologia e Cirurgia Vascular", "vascular"), ("Oftalmologia", "oftalmologia"),
            ("Pediatria", "pediatria"), ("Nutrição", "nutricao"), ("Pacote de consulta", "consulta")
        };
        await using var tx = await connection.BeginTransactionAsync(ct);
        foreach (var (name, file) in icons)
        {
            await using var stream = typeof(PacoteIcone).Assembly.GetManifestResourceStream($"MdwConteudos.Api.Assets.Pacotes.{file}.png")
                ?? throw new InvalidOperationException($"Icone inicial ausente: {file}");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            var icon = new TextBlob(Validate(Prefix + Convert.ToBase64String(buffer.ToArray())));
            await connection.ExecuteAsync(new CommandDefinition("UPDATE PACOTES SET ICONE_BASE64=@icon WHERE UPPER(TRIM(NOME))=UPPER(@name) AND (ICONE_BASE64 IS NULL OR OCTET_LENGTH(ICONE_BASE64)=0)", new { icon, name }, tx, cancellationToken: ct));
        }
        await tx.CommitAsync(ct);
    }
}
