using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MdwConteudos.Api.Modules.Automacoes;

// Explicit local opt-in; never fallback from live reports to demonstration data.
public static class ChatfastDemo
{
    public static async Task<ChatfastReportRequest> Read(string type, IHostEnvironment environment, IOptions<ChatfastOptions> options, CancellationToken ct)
    {
        if (!environment.IsDevelopment() || string.IsNullOrWhiteSpace(options.Value.DemoDirectory) || type is not ("diario" or "semanal"))
            throw new ChatfastException(404, "Demonstração local indisponível.");
        var directory = options.Value.DemoDirectory;
        if (!Path.IsPathFullyQualified(directory)) throw new ChatfastException(503, "Configure o diretório absoluto da demonstração local.");
        var path = Path.Combine(directory, $"{type}.json");
        if (!File.Exists(path) || new FileInfo(path).Length > 1_048_576) throw new ChatfastException(404, "Arquivo local de demonstração não preparado.");
        var report = JsonSerializer.Deserialize<ChatfastReportRequest>(await File.ReadAllTextAsync(path, ct), ChatfastContract.Json)
            ?? throw new ChatfastException(400, "Demonstração inválida.");
        ChatfastContract.Validate(report);
        if (report.Type != type) throw new ChatfastException(400, "Tipo da demonstração incompatível.");
        return report;
    }
}
