using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
namespace ConversorHtml.Application.Interfaces;

public record VoiceApplyResult(string CamposScriptJson, string Summary, IReadOnlyList<string> Warnings);

public record VoiceExportResult(string Content, ConversionOutputFormat Format);

public interface IVoiceLaudoService
{
    Task<string> BootstrapFromImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
    Task<VoiceApplyResult> ApplyUtteranceAsync(
        VoiceSession session,
        string transcript,
        VoiceUtteranceIntent intent = VoiceUtteranceIntent.Build,
        CancellationToken cancellationToken = default);
    Task<VoiceExportResult> ExportAsync(VoiceSession session, ConversionOutputFormat format, CancellationToken cancellationToken = default);
}
