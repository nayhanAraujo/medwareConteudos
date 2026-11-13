using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Modules.RealtimeVoice;

namespace MdwConteudos.Api.Modules.StudioDrafts;

[ApiController, Authorize, Route("api/voice/capabilities")]
[RequirePermission("studio", "visualizar"), RequirePermission("studio", "voz")]
public sealed class StudioVoiceCapabilitiesController(StudioDraftService drafts, IConfiguration config, IOptions<RealtimeVoiceOptions> realtime,
    ILogger<StudioVoiceCapabilitiesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var persistent = false;
        try { persistent = await drafts.Available(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Studio drafts unavailable during capability check: {ErrorType}",ex.GetType().Name); }
        var keyExists = !string.IsNullOrWhiteSpace(realtime.Value.ApiKey) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XAI_API_KEY"));
        return Ok(new {guidedVoice = config.GetValue<bool>("StudioVoiceUx:Enabled"), persistentDrafts = persistent,
            realtimeVoice = realtime.Value.Enabled && keyExists, conventionalVoice = true});
    }
}
