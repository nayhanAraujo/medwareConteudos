using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Modules.FirebirdAdmin;

public interface IFirebirdAdminService
{
    FirebirdStatusResult GetStatus();
    Task<FirebirdTestResult> TestConnectionAsync(FirebirdConnectionRequest request, CancellationToken ct);
    Task<FirebirdProbeResult> RunProbeAsync(FirebirdConnectionRequest? connection, CancellationToken ct);
    Task<FirebirdSqlResult> ExecuteSqlAsync(FirebirdSqlRequest request, CancellationToken ct);
}

public sealed class FirebirdAdminService(
    IFirebirdConnectionFactory connectionFactory,
    IOptions<FirebirdOptions> options) : IFirebirdAdminService
{
    private const int MaximumAllowedRows = 500;
    private const string ProbeSql = "SELECT 1 AS RESULTADO FROM RDB$DATABASE";
    private static readonly Regex AllowedStart = new(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ForbiddenCommand = new(
        @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|EXECUTE|GRANT|REVOKE|COMMIT|ROLLBACK|MERGE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly FirebirdOptions _options = options.Value;

    public FirebirdStatusResult GetStatus()
    {
        var configured = !string.IsNullOrWhiteSpace(_options.Host)
            && _options.Port is > 0 and <= 65535
            && !string.IsNullOrWhiteSpace(_options.Database)
            && !string.IsNullOrWhiteSpace(_options.User);

        if (!configured)
            return new FirebirdStatusResult(false, null, null);

        var connection = ToPublicConnection(_options);
        return new FirebirdStatusResult(true, BuildSummary(connection), connection);
    }

    public async Task<FirebirdTestResult> TestConnectionAsync(FirebirdConnectionRequest request, CancellationToken ct)
    {
        ValidateConnection(request);
        await using var opened = await OpenAsync(request, ct);
        var connection = new FirebirdPublicConnection(
            request.Host.Trim(), request.Port, request.Database.Trim(), request.User.Trim(), opened.Charset);

        return new FirebirdTestResult(
            "Conexão Firebird validada com sucesso.",
            BuildSummary(connection),
            connection);
    }

    public async Task<FirebirdProbeResult> RunProbeAsync(FirebirdConnectionRequest? connection, CancellationToken ct)
    {
        await using var opened = await OpenAsync(connection, ct);
        var result = await opened.Connection.QuerySingleOrDefaultAsync<object?>(
            new CommandDefinition(ProbeSql, cancellationToken: ct));
        return new FirebirdProbeResult(
            "Consulta de teste executada com sucesso.", ProbeSql, NormalizeValue(result), opened.Charset);
    }

    public async Task<FirebirdSqlResult> ExecuteSqlAsync(FirebirdSqlRequest request, CancellationToken ct)
    {
        var sql = ValidateSql(request.Sql);
        if (request.MaxRows is < 1 or > MaximumAllowedRows)
            throw new FirebirdAdminValidationException($"O limite de linhas deve estar entre 1 e {MaximumAllowedRows}.");

        var parameters = request.Params ?? [];
        var parameterizedSql = BindPositionalParameters(sql, parameters.Count);
        var dapperParameters = BuildParameters(parameters);

        await using var opened = await OpenAsync(request.Connection, ct);
        await using var transaction = await opened.Connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var columns = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;

        try
        {
            await using var reader = await opened.Connection.ExecuteReaderAsync(
                new CommandDefinition(parameterizedSql, dapperParameters, transaction, cancellationToken: ct));

            if (reader.FieldCount == 0)
                throw new FirebirdAdminValidationException("A consulta deve retornar dados.");

            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = reader.GetName(index)?.Trim();
                columns.Add(string.IsNullOrWhiteSpace(name) ? $"COL_{index + 1}" : name);
            }

            while (await reader.ReadAsync(ct))
            {
                if (rows.Count == request.MaxRows)
                {
                    truncated = true;
                    break;
                }

                var row = new object?[reader.FieldCount];
                for (var index = 0; index < reader.FieldCount; index++)
                    row[index] = reader.IsDBNull(index) ? null : NormalizeValue(reader.GetValue(index));
                rows.Add(row);
            }
        }
        finally
        {
            await transaction.RollbackAsync(ct);
        }

        return new FirebirdSqlResult(
            "Consulta SQL executada com sucesso.", sql, columns, rows,
            rows.Count, truncated, request.MaxRows, opened.Charset);
    }

    private async Task<OpenedConnection> OpenAsync(FirebirdConnectionRequest? request, CancellationToken ct)
    {
        if (request is null)
        {
            var configuredConnection = await connectionFactory.OpenConnectionAsync(ct);
            return new OpenedConnection(configuredConnection, ResolveConfiguredCharset(configuredConnection));
        }

        ValidateConnection(request);
        Exception? firstFailure = null;
        foreach (var charset in CharsetsToTry(request.Charset))
        {
            var connection = new FbConnection(BuildConnectionString(request, charset));
            try
            {
                await connection.OpenAsync(ct);
                return new OpenedConnection(connection, charset);
            }
            catch (Exception ex)
            {
                firstFailure ??= ex;
                await connection.DisposeAsync();
            }
        }

        throw new FirebirdAdminConnectionException("Não foi possível conectar ao Firebird com os dados informados.", firstFailure);
    }

    private static void ValidateConnection(FirebirdConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.Database)
            || string.IsNullOrWhiteSpace(request.User) || string.IsNullOrWhiteSpace(request.Password))
            throw new FirebirdAdminValidationException("Host, porta, banco, usuário e senha são obrigatórios.");
        if (request.Port is < 1 or > 65535)
            throw new FirebirdAdminValidationException("A porta deve estar entre 1 e 65535.");
    }

    private static string ValidateSql(string? value)
    {
        var sql = value?.Trim() ?? string.Empty;
        if (sql.Length == 0)
            throw new FirebirdAdminValidationException("Informe a consulta SQL.");
        if (sql.Contains(';', StringComparison.Ordinal))
            throw new FirebirdAdminValidationException("Apenas uma consulta por vez é permitida.");
        if (!AllowedStart.IsMatch(sql))
            throw new FirebirdAdminValidationException("Apenas consultas de leitura (SELECT ou WITH) são permitidas.");
        if (ForbiddenCommand.IsMatch(sql))
            throw new FirebirdAdminValidationException("A consulta contém comandos não permitidos.");
        return sql;
    }

    private static string BindPositionalParameters(string sql, int parameterCount)
    {
        var builder = new StringBuilder(sql.Length + parameterCount * 3);
        var inString = false;
        var parameterIndex = 0;
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            if (current == '\'' && index + 1 < sql.Length && sql[index + 1] == '\'' && inString)
            {
                builder.Append("''");
                index++;
                continue;
            }
            if (current == '\'') inString = !inString;
            if (current == '?' && !inString)
            {
                if (parameterIndex >= parameterCount)
                    throw new FirebirdAdminValidationException("Há parâmetros posicionais sem valor correspondente.");
                builder.Append("@p").Append(parameterIndex++);
            }
            else
            {
                builder.Append(current);
            }
        }

        if (parameterIndex != parameterCount)
            throw new FirebirdAdminValidationException("A quantidade de parâmetros não corresponde aos marcadores '?'.");
        return builder.ToString();
    }

    private static DynamicParameters BuildParameters(IReadOnlyList<JsonElement> parameters)
    {
        var result = new DynamicParameters();
        for (var index = 0; index < parameters.Count; index++)
            result.Add($"p{index}", ConvertJsonValue(parameters[index]));
        return result;
    }

    private static object? ConvertJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String when value.TryGetDateTime(out var date) => date,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new FirebirdAdminValidationException("Os parâmetros aceitam apenas valores simples.")
    };

    private static object? NormalizeValue(object? value) => value switch
    {
        null or DBNull => null,
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString(),
        _ => value
    };

    private static string BuildConnectionString(FirebirdConnectionRequest request, string charset)
    {
        var builder = new FbConnectionStringBuilder
        {
            DataSource = request.Host.Trim(),
            Port = request.Port,
            Database = request.Database.Trim(),
            UserID = request.User.Trim(),
            Password = request.Password,
            Charset = charset,
            Dialect = 3,
            Pooling = false
        };
        return builder.ConnectionString;
    }

    private static IEnumerable<string> CharsetsToTry(string? requested)
    {
        var first = string.IsNullOrWhiteSpace(requested) ? "UTF8" : requested.Trim().ToUpperInvariant();
        yield return first;
        if (!first.Equals("ISO8859_1", StringComparison.OrdinalIgnoreCase))
            yield return "ISO8859_1";
    }

    private string ResolveConfiguredCharset(FbConnection connection)
        => string.IsNullOrWhiteSpace(_options.Charset) ? "UTF8" : _options.Charset.Trim().ToUpperInvariant();

    private static FirebirdPublicConnection ToPublicConnection(FirebirdOptions options)
        => new(options.Host, options.Port, options.Database, options.User,
            string.IsNullOrWhiteSpace(options.Charset) ? "UTF8" : options.Charset);

    private static string BuildSummary(FirebirdPublicConnection connection)
        => $"{connection.User}@{connection.Host}:{connection.Port} · {connection.Database}";

    private sealed class OpenedConnection(FbConnection connection, string charset) : IAsyncDisposable
    {
        public FbConnection Connection { get; } = connection;
        public string Charset { get; } = charset;
        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}

public sealed class FirebirdAdminValidationException(string message) : Exception(message);

public sealed class FirebirdAdminConnectionException(string message, Exception? innerException = null)
    : Exception(message, innerException);

