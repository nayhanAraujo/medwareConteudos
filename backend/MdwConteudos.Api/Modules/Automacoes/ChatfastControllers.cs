using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using MdwConteudos.Api.Modules.Auditoria;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;

namespace MdwConteudos.Api.Modules.Automacoes;

public sealed class ChatfastErrors(ILogger<ChatfastErrors> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception is ChatfastException e ? e.Status : 503;
        var message = context.Exception is ChatfastException known ? known.Message : "ChatFast indisponível. Verifique a configuração e o esquema do módulo.";
        // Do not log payloads, observations, addresses, credentials or database messages.
        if (context.Exception is not ChatfastException) logger.LogWarning("ChatFastFailure Type={Type} TraceId={TraceId}", context.Exception.GetType().Name, context.HttpContext.TraceIdentifier);
        if (context.Exception is FbException fb && fb.SQLSTATE == "23000") { status = 409; message = "Concorrência na gravação. Repita a mesma solicitação."; }
        context.Result = new ObjectResult(new { success = false, message, traceId = context.HttpContext.TraceIdentifier }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

[ApiController, AllowAnonymous, Route("api/integracoes/chatfast/v1"), ServiceFilter(typeof(ChatfastErrors)), EnableRateLimiting("chatfast"), RequestSizeLimit(1_048_576)]
public sealed class ChatfastIntegrationController(ChatfastService service) : ControllerBase
{
    void Authenticate(bool reports)
    {
        if (!Request.IsHttps && !HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            throw new ChatfastException(400, "A integração exige HTTPS.");
        service.Authenticate(Request.Headers["X-Chatfast-Key"].ToString(), reports);
        Response.Headers.CacheControl = "no-store";
    }
    [HttpPost("respostas")]
    public async Task<IActionResult> ResponseEvent(ChatfastResponseRequest request, CancellationToken ct)
    { Authenticate(false); return Ok(await service.Receive(ChatfastContract.Parse(request), ct)); }
    [HttpPost("relatorios")]
    public async Task<IActionResult> Report(ChatfastReportRequest request, CancellationToken ct)
    { Authenticate(true); return Ok(await service.Report(request, ct)); }
}

[ApiController, AllowAnonymous, Route("api/publico/chatfast/v1"), ServiceFilter(typeof(ChatfastErrors)), EnableRateLimiting("chatfast-public")]
public sealed class ChatfastPublicController(ChatfastService service) : ControllerBase
{
    [HttpGet("demonstracao/{type}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Demo(string type, [FromServices] IHostEnvironment environment, [FromServices] IOptions<ChatfastOptions> options, CancellationToken ct)
    {
        var report = await ChatfastDemo.Read(type, environment, options, ct);
        return Ok(new { success = true, data = ChatfastContract.Public(report, DateTimeOffset.UtcNow), demonstration = true });
    }
    [HttpGet("relatorios/{type}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Report(string type, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var report = await service.Public(type, date, ct);
        return Ok(new { success = true, data = report });
    }
}

[ApiController, Authorize(Roles = "admin"), Route("api/web/automacoes/chatfast"), ServiceFilter(typeof(ActiveAuditAdminFilter)), ServiceFilter(typeof(ChatfastErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChatfastAdminController(ChatfastService service) : ControllerBase
{
    [HttpGet("demonstracao/{type}")]
    public async Task<IActionResult> Demo(string type, [FromServices] IHostEnvironment environment, [FromServices] IOptions<ChatfastOptions> options, CancellationToken ct) =>
        Ok(new { success = true, data = await ChatfastDemo.Read(type, environment, options, ct), demonstration = true });
    [HttpGet("{kind:regex(^(respostas|relatorios)$)}")]
    public async Task<IActionResult> List(string kind, [FromQuery] long? before, CancellationToken ct) => Ok(await service.List(kind, before, ct));

    [HttpPost("importacoes"), RequestSizeLimit(5_500_000), RequestFormLimits(MultipartBodyLengthLimit = 5_500_000)]
    public async Task<IActionResult> Import([FromForm] IFormFile file, [FromForm] bool confirm, CancellationToken ct)
    {
        service.Enabled();
        if (file.Length is 0 or > 5_242_880) throw new ChatfastException(400, "Envie uma planilha de até 5 MB.");
        using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct);
        var request = ChatfastSpreadsheet.Read(stream.ToArray(), file.FileName, confirm);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        return Ok(await ChatfastService.Retry(() => service.Import(request, actor, ct), ct));
    }
}
