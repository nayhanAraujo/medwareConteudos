using System.Text.Json;

namespace MdwConteudos.Api.Modules.FirebirdAdmin;

public sealed record FirebirdConnectionRequest(
    string Host,
    int Port,
    string Database,
    string User,
    string Password,
    string? Charset = null);

public sealed record FirebirdConnectionContextRequest(FirebirdConnectionRequest? Connection = null);

public sealed record FirebirdSqlRequest(
    string Sql,
    IReadOnlyList<JsonElement>? Params = null,
    int MaxRows = 100,
    FirebirdConnectionRequest? Connection = null);

public sealed record FirebirdPublicConnection(
    string Host,
    int Port,
    string Database,
    string User,
    string Charset);

public sealed record FirebirdStatusResult(
    bool Configured,
    string? Summary,
    FirebirdPublicConnection? Connection);

public sealed record FirebirdTestResult(
    string Message,
    string Summary,
    FirebirdPublicConnection Connection);

public sealed record FirebirdProbeResult(
    string Message,
    string Query,
    object? Resultado,
    string Charset);

public sealed record FirebirdSqlResult(
    string Message,
    string Sql,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int RowCount,
    bool Truncated,
    int MaxRows,
    string Charset);

