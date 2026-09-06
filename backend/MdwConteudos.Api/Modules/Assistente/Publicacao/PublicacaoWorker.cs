namespace MdwConteudos.Api.Modules.Assistente.Publicacao;

public sealed class PublicacaoWorker(IServiceScopeFactory scopes, ILogger<PublicacaoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<PublicacaoService>();
                if (service.Enabled && await service.ProcessNext(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha no processamento da publicacao no Assistente."); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
