using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ConversorHtml.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConversorHtml.Application.Services;

internal static class VoiceBridgeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<T> RunJsonAsync<T>(
        IOptions<CursorOptions> cursorOptions,
        IOptions<VoiceOptions> voiceOptions,
        ILogger logger,
        string command,
        object payload,
        CancellationToken cancellationToken)
    {
        var cursor = cursorOptions.Value;
        var voice = voiceOptions.Value;

        var apiKey = cursor.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("CURSOR_API_KEY")?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Cursor API key não configurada. Defina Cursor:ApiKey ou CURSOR_API_KEY.");
        }

        var bridgePath = ResolveBridgePath(cursor, voice);
        if (!File.Exists(bridgePath))
        {
            throw new FileNotFoundException($"Bridge voice não encontrado em '{bridgePath}'.", bridgePath);
        }

        var repoRoot = ResolveRepoRoot(bridgePath, cursor);
        var payloadPath = Path.Combine(Path.GetTempPath(), $"voice-payload-{Guid.NewGuid():N}.json");
        var timeout = TimeSpan.FromSeconds(Math.Max(30, cursor.TimeoutSeconds));

        try
        {
            await File.WriteAllTextAsync(payloadPath, JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{bridgePath}\" \"{command}\" \"{payloadPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(bridgePath) ?? repoRoot
            };

            startInfo.Environment["CURSOR_API_KEY"] = apiKey;
            startInfo.Environment["CURSOR_MODEL"] = cursor.Model;
            startInfo.Environment["CURSOR_USE_FAST"] = cursor.UseFast ? "true" : "false";
            startInfo.Environment["CURSOR_REPO_ROOT"] = repoRoot;

            using var process = new Process { StartInfo = startInfo };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) stderr.AppendLine(e.Data);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Não foi possível iniciar o processo Node.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }

                throw new TimeoutException($"Voice bridge excedeu timeout de {cursor.TimeoutSeconds}s.");
            }

            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(stderr.ToString()) ? stdout.ToString() : stderr.ToString();
                logger.LogError("Voice bridge falhou ({Command}, exit {Exit}): {Detail}", command, process.ExitCode, detail);
                throw new InvalidOperationException($"Voice bridge falhou: {Truncate(detail, 500)}");
            }

            var output = stdout.ToString().Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException("Voice bridge retornou saída vazia.");
            }

            return JsonSerializer.Deserialize<T>(output, JsonOptions)
                   ?? throw new InvalidOperationException("Voice bridge retornou JSON inválido.");
        }
        finally
        {
            try
            {
                if (File.Exists(payloadPath)) File.Delete(payloadPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string ResolveBridgePath(CursorOptions cursor, VoiceOptions voice)
    {
        var configured = voice.VoiceBridgeScriptPath;
        if (Path.IsPathRooted(configured) && File.Exists(configured))
        {
            return configured;
        }

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", configured)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", configured)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configured)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", configured))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", configured));
    }

    private static string ResolveRepoRoot(string bridgePath, CursorOptions cursor)
    {
        if (!string.IsNullOrWhiteSpace(cursor.RepoRoot) && Directory.Exists(cursor.RepoRoot))
        {
            return Path.GetFullPath(cursor.RepoRoot);
        }

        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bridgePath)!, ".."));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
