using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Configuration;

namespace MdwConteudos.Api.Infrastructure;

public interface IAssistantFirebirdConnectionFactory
{
    Task<FbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class AssistantFirebirdConnectionFactory : IAssistantFirebirdConnectionFactory
{
    private readonly AssistantFirebirdOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AssistantFirebirdConnectionFactory> _logger;

    public AssistantFirebirdConnectionFactory(
        IOptions<AssistantFirebirdOptions> options,
        IHostEnvironment environment,
        ILogger<AssistantFirebirdConnectionFactory> logger)
    {
        _options = options.Value;
        _options.ApplyEnvironmentVariables();
        _options.Validate();
        _environment = environment;
        _logger = logger;
    }

    public async Task<FbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new FbConnection(_options.BuildConnectionString(_environment.ContentRootPath));
        try
        {
            await connection.OpenAsync(cancellationToken);
            _logger.LogDebug("Conexão com o banco Assistente aberta em {Host}:{Port}.", _options.Host, _options.Port);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            _logger.LogWarning("Não foi possível abrir a conexão com o banco Assistente em {Host}:{Port}.", _options.Host, _options.Port);
            throw;
        }
    }
}
