namespace MdwConteudos.Api.Configuration;

public sealed class AssistantFirebirdOptions
{
    public const string SectionName = "AssistantFirebird";
    public const string EnvironmentPrefix = "ASSISTENTE_FIREBIRD_";
    public const string DefaultDatabase = "bd_assistente/ASSISTENTE/ASSISTENTE.FDB";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3050;
    public string Database { get; set; } = DefaultDatabase;
    public string User { get; set; } = "SYSDBA";
    public string Password { get; set; } = string.Empty;
    public string Charset { get; set; } = "ISO8859_1";
    public string? ClientLibrary { get; set; }

    public void ApplyEnvironmentVariables(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        Host = Read(read, "HOST", Host);
        Database = Read(read, "DATABASE", Database);
        User = Read(read, "USER", User);
        Password = Read(read, "PASSWORD", Password);
        Charset = Read(read, "CHARSET", Charset);
        ClientLibrary = ReadOptional(read, "CLIENT_LIBRARY", ClientLibrary);

        var port = read(EnvironmentPrefix + "PORT");
        if (!string.IsNullOrWhiteSpace(port))
        {
            if (!int.TryParse(port, out var parsed) || parsed is < 1 or > 65535)
                throw new InvalidOperationException("ASSISTENTE_FIREBIRD_PORT deve ser uma porta TCP válida.");
            Port = parsed;
        }
    }

    public string BuildConnectionString(string? databaseRoot = null)
    {
        Validate();
        var database = ResolveDatabasePath(databaseRoot).Replace('\\', '/');
        var builder = new FirebirdSql.Data.FirebirdClient.FbConnectionStringBuilder
        {
            DataSource = Host,
            Port = Port,
            Database = database,
            UserID = User,
            Password = Password,
            Charset = Charset,
            Dialect = 3,
            // No provider Firebird .NET, Embedded seleciona a implementação
            // nativa quando ClientLibrary é informada. DataSource/Port continuam
            // determinando a conexão remota feita pelo fbclient.dll.
            ServerType = string.IsNullOrWhiteSpace(ClientLibrary)
                ? FirebirdSql.Data.FirebirdClient.FbServerType.Default
                : FirebirdSql.Data.FirebirdClient.FbServerType.Embedded
        };
        if (!string.IsNullOrWhiteSpace(ClientLibrary))
            builder.ClientLibrary = Path.GetFullPath(ClientLibrary);
        return builder.ToString();
    }

    public string ResolveDatabasePath(string? databaseRoot = null)
    {
        if (Path.IsPathFullyQualified(Database)) return Path.GetFullPath(Database);
        return Path.GetFullPath(Database, databaseRoot ?? Directory.GetCurrentDirectory());
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new InvalidOperationException("Host do banco Assistente não configurado.");
        if (Port is < 1 or > 65535) throw new InvalidOperationException("Porta do banco Assistente inválida.");
        if (string.IsNullOrWhiteSpace(Database)) throw new InvalidOperationException("Caminho do banco Assistente não configurado.");
        if (string.IsNullOrWhiteSpace(User)) throw new InvalidOperationException("Usuário do banco Assistente não configurado.");
        if (!string.Equals(Charset, "ISO8859_1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O banco Assistente deve utilizar o charset ISO8859_1.");
        if (!string.IsNullOrWhiteSpace(ClientLibrary))
        {
            if (!Path.IsPathFullyQualified(ClientLibrary))
                throw new InvalidOperationException("A biblioteca cliente do banco Assistente deve usar um caminho absoluto.");
            if (!File.Exists(ClientLibrary))
                throw new InvalidOperationException("A biblioteca cliente configurada para o banco Assistente não foi encontrada.");
        }
    }

    public override string ToString() =>
        $"AssistantFirebirdOptions {{ Host = {Host}, Port = {Port}, Database = {Database}, User = {User}, Charset = {Charset}, ClientLibrary = {ClientLibrary ?? "(managed)"}, Password = *** }}";

    private static string Read(Func<string, string?> read, string suffix, string current)
    {
        var value = read(EnvironmentPrefix + suffix);
        return string.IsNullOrWhiteSpace(value) ? current : value.Trim();
    }

    private static string? ReadOptional(Func<string, string?> read, string suffix, string? current)
    {
        var value = read(EnvironmentPrefix + suffix);
        return string.IsNullOrWhiteSpace(value) ? current : value.Trim();
    }
}
