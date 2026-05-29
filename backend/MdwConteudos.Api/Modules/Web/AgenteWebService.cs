using Microsoft.AspNetCore.Mvc;

namespace MdwConteudos.Api.Modules.Web;

public interface IAgenteWebService
{
    Task<object> StatusAsync(CancellationToken ct);
}

public class AgenteWebService : IAgenteWebService
{
    private readonly IConfiguration _config;
    public AgenteWebService(IConfiguration config) => _config = config;

    public Task<object> StatusAsync(CancellationToken ct) => Task.FromResult<object>(new
    {
        success = true,
        grok_configured = !string.IsNullOrEmpty(_config["GROK_API_KEY"] ?? _config["XAI_API_KEY"]),
        pubmed = true,
        message = "Agente disponível via endpoints dedicados na migração."
    });
}

[ApiController]
[Route("api/web/agente")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class AgenteWebController : ControllerBase
{
    private readonly IAgenteWebService _svc;
    public AgenteWebController(IAgenteWebService svc) => _svc = svc;
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await _svc.StatusAsync(ct));
}
