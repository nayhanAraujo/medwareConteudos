namespace MdwConteudos.Api.Configuration;

public class FirebirdOptions
{
    public const string SectionName = "Firebird";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3052;
    public string Database { get; set; } = "";
    public string User { get; set; } = "SYSDBA";
    public string Password { get; set; } = "";
    public string Charset { get; set; } = "UTF8";

    public string BuildConnectionString(string? charsetOverride = null)
    {
        return new FirebirdSql.Data.FirebirdClient.FbConnectionStringBuilder
        {
            DataSource = Host, Port = Port, Database = Database.Replace('\\', '/'),
            UserID = User, Password = Password, Charset = charsetOverride ?? Charset
        }.ToString();
    }
}
