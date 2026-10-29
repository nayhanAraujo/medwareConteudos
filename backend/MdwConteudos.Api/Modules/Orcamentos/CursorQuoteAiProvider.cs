using System.Diagnostics;
using System.Text;
using ConversorHtml.Application.Configuration;
using Microsoft.Extensions.Options;

namespace MdwConteudos.Api.Modules.Orcamentos;

public sealed class CursorQuoteAiProvider(IOptions<OrcamentoAiOptions> options, IOptions<CursorOptions> cursor, IWebHostEnvironment environment) : IQuoteAiProvider
{
    public async Task<QuoteAiSuggestion> Analyze(QuoteAiInput input, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled || string.IsNullOrWhiteSpace(cursor.Value.ApiKey)) throw new OrcamentoException(503, "Análise por IA não configurada.");
        var script = Path.GetFullPath(Path.Combine(environment.ContentRootPath, o.BridgePath));
        if (!File.Exists(script)) throw new OrcamentoException(503, "Bridge de IA não instalado.");
        var directory = Directory.CreateTempSubdirectory("mdw-quote-ai-");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 15, 300)));
        using var process = new Process();
        try
        {
            var start = new ProcessStartInfo(o.NodePath) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory.FullName,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false, true),
                StandardOutputEncoding = new UTF8Encoding(false, true),
                StandardErrorEncoding = new UTF8Encoding(false, true) };
            start.ArgumentList.Add(script);
            // Do not propagate appsettings, Firebird passwords, proxy credentials, NODE_OPTIONS or user settings.
            start.Environment.Clear();
            foreach (var key in new[] { "SystemRoot", "WINDIR", "PATH" }) if (Environment.GetEnvironmentVariable(key) is { } value) start.Environment[key] = value;
            foreach (var key in new[] { "TEMP", "TMP", "TMPDIR", "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA" }) start.Environment[key] = directory.FullName;
            start.Environment["CURSOR_API_KEY"] = cursor.Value.ApiKey; start.Environment["QUOTE_AI_MODEL"] = o.Model;
            process.StartInfo = start;
            if (!process.Start()) throw new OrcamentoException(503, "Executor de IA indisponível.");
            var output = ReadBounded(process.StandardOutput, 110_000, timeout.Token);
            var error = ReadBounded(process.StandardError, 20_000, timeout.Token);
            await process.StandardInput.WriteAsync(OrcamentoJson.Write(input).AsMemory(), timeout.Token); process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var result = await output; _ = await error;
            if (process.ExitCode != 0) throw new OrcamentoException(502, "O Composer não concluiu a análise. Confira a instalação e a credencial no servidor.");
            return QuoteAiSuggestion.Parse(result);
        }
        catch (DecoderFallbackException)
        {
            throw new OrcamentoException(502, "O executor de IA retornou texto com codificação inválida. Nenhum item foi aplicado.");
        }
        finally
        {
            try { if (process.Id > 0 && !process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } } catch (InvalidOperationException) { }
            try { directory.Delete(recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static async Task<string> ReadBounded(StreamReader reader, int max, CancellationToken ct)
    {
        var output = new StringBuilder(); var chars = new char[4096]; int count;
        while ((count = await reader.ReadAsync(chars, ct)) > 0) { output.Append(chars, 0, count); if (output.Length > max) throw new OrcamentoException(502, "Saída do executor excedeu o limite."); }
        return output.ToString();
    }
}
