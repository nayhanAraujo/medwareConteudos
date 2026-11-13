using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
namespace ConversorHtml.Application.Interfaces;

public record VoiceApplyResult(string CamposScriptJson, string Summary, IReadOnlyList<string> Warnings, long? Revision = null, string? DocumentHash = null);

public record VoiceExportResult(string Content, ConversionOutputFormat Format);
public sealed record VoiceStructureCall(string Name, System.Text.Json.JsonElement Arguments);
public sealed record VoiceStructurePlan(IReadOnlyList<VoiceStructureCall> Commands, string Summary, IReadOnlyList<string> Warnings);

public interface IVoiceLaudoService
{
    Task<VoiceStructurePlan> PlanStructureAsync(object document, object catalog, string transcript, CancellationToken ct = default)
        => throw new InvalidOperationException("O provedor configurado não oferece edição estruturada. Utilize o modo de voz em tempo real.");
    Task<string> BootstrapFromImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
    Task<VoiceApplyResult> ApplyUtteranceAsync(
        VoiceSession session,
        string transcript,
        VoiceUtteranceIntent intent = VoiceUtteranceIntent.Build,
        CancellationToken cancellationToken = default);
    Task<VoiceExportResult> ExportAsync(VoiceSession session, ConversionOutputFormat format, CancellationToken cancellationToken = default);
}
