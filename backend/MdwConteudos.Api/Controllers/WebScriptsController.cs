using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MdwConteudos.Api.Services;

namespace MdwConteudos.Api.Controllers;

[ApiController]
[Route("api/web/scripts")]
public class WebScriptsController : ControllerBase
{
    private readonly ScriptsService _scripts;

    public WebScriptsController(ScriptsService scripts) => _scripts = scripts;

    private bool IsAdmin => User.IsInRole("admin");

  [HttpGet("pacotes")]
    [Authorize]
    public async Task<IActionResult> GetPacotes()
    {
        var data = await _scripts.GetPacotesAsync();
        return Ok(new { success = true, data });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(
        [FromQuery] string? nome,
        [FromQuery] string? sistema,
        [FromQuery] int? pacote,
        [FromQuery] int? aprovado,
        [FromQuery] int? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _scripts.ListScriptsAsync(nome, sistema, pacote, aprovado, ativo, page, pageSize);
        return Ok(new { success = true, data = result.Items, page = result.Page, totalPages = result.TotalPages, totalItems = result.TotalItems });
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Get(int id)
    {
        var s = await _scripts.GetScriptAsync(id);
        if (s == null) return NotFound(new { message = "Script não encontrado." });
        return Ok(new { success = true, data = s });
    }

    [HttpPost("verificar-nome")]
    [Authorize]
    public async Task<IActionResult> VerificarNome([FromBody] VerificarNomeRequest req)
    {
        var exists = await _scripts.VerificarNomeAsync(req.Nome, req.ExcludeId);
        return Ok(new { success = true, exists });
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Create([FromForm] ScriptFormDto form)
    {
        try
        {
            var input = await MapFormAsync(form);
            var id = await _scripts.CreateScriptAsync(input);
            return Ok(new { success = true, codScriptLaudo = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Update(int id, [FromForm] ScriptFormDto form)
    {
        try
        {
            var input = await MapFormAsync(form);
            await _scripts.UpdateScriptAsync(id, input);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/toggle-ativo")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ToggleAtivo(int id)
    {
        await _scripts.ToggleAtivoAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("{id:int}/toggle-aprovacao")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ToggleAprovacao(int id)
    {
        var nome = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
        await _scripts.ToggleAprovacaoAsync(id, nome);
        return Ok(new { success = true });
    }

    [HttpGet("{id:int}/variaveis")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetVariaveis(int id)
    {
        var data = await _scripts.GetVariaveisForLinkAsync(id);
        var script = await _scripts.GetScriptAsync(id);
        return Ok(new { success = true, data, nomeScript = script?.Nome });
    }

    [HttpPost("{id:int}/variaveis")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SaveVariaveis(int id, [FromBody] SaveVariaveisRequest req)
    {
        await _scripts.SaveVariaveisAsync(id, req.CodVariaveis ?? []);
        return Ok(new { success = true });
    }

    [HttpGet("{id:int}/mrd")]
    [Authorize]
    public async Task<IActionResult> GetMrd(int id)
    {
        var s = await _scripts.GetScriptAsync(id);
        if (s == null) return NotFound();
        return Ok(new { success = true, data = s.MrdList, nomeScript = s.Nome, sistema = s.Sistema });
    }

    [HttpPost("{id:int}/mrd")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> AddMrd(int id, [FromForm] string sistema, [FromForm] List<IFormFile> arquivos_mrd)
    {
        try
        {
            var files = await ReadFilesAsync(arquivos_mrd);
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "Nenhum arquivo enviado." });
            await _scripts.AddMrdFilesOnlyAsync(id, sistema, files);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("mrd/{codScriptMrd:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteMrd(int codScriptMrd)
    {
        await _scripts.DeleteMrdAsync(codScriptMrd);
        return Ok(new { success = true });
    }

    [HttpGet("{id:int}/versoes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ListVersoes(
        int id,
        [FromQuery] string? numeroVersao,
        [FromQuery] string? aprovado,
        [FromQuery] string? ativo)
    {
        var data = await _scripts.ListVersoesAsync(id, numeroVersao, aprovado, ativo);
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:int}/versoes/meta")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetVersaoCreateMeta(int id)
    {
        var data = await _scripts.GetVersaoCreateMetaAsync(id);
        if (data == null) return NotFound(new { message = "Script não encontrado." });
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:int}/versoes/{codVersao:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetVersao(int id, int codVersao)
    {
        var data = await _scripts.GetVersaoAsync(id, codVersao);
        if (data == null) return NotFound(new { message = "Versão não encontrada." });
        return Ok(new { success = true, data });
    }

    [HttpPost("{id:int}/versoes")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> CreateVersao(int id, [FromForm] VersaoFormDto form)
    {
        try
        {
            var input = await MapVersaoFormAsync(form);
            input.UsuarioResponsavel = FirstUserName(input.UsuarioResponsavel);
            var cod = await _scripts.CreateVersaoAsync(id, input);
            return Ok(new { success = true, codVersao = cod });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("versoes/{codVersao:int}")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UpdateVersao(int codVersao, [FromForm] VersaoFormDto form)
    {
        try
        {
            var input = await MapVersaoFormAsync(form);
            input.UsuarioResponsavel = FirstUserName(input.UsuarioResponsavel);
            await _scripts.UpdateVersaoAsync(codVersao, input);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("versoes/{codVersao:int}/ativar")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AtivarVersao(int codVersao)
    {
        try
        {
            var user = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";
            await _scripts.ActivateVersaoAsync(codVersao, user);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("versoes/{codVersao:int}/aprovar")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AprovarVersao(int codVersao)
    {
        try
        {
            var user = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";
            await _scripts.ApproveVersaoAsync(codVersao, user);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("versoes/{codVersao:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteVersao(int codVersao)
    {
        try
        {
            var user = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";
            await _scripts.DeleteVersaoAsync(codVersao, user);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("versoes/{codVersao:int}/exportar/{tipo}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExportVersaoArquivo(int codVersao, string tipo)
    {
        var r = await _scripts.ExportVersaoArquivoAsync(codVersao, tipo);
        if (r == null) return NotFound(new { message = "Arquivo não encontrado." });
        return File(r.Value.content, r.Value.mime, r.Value.filename);
    }

    [HttpGet("versoes/{codVersao:int}/exportar-mrd")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExportVersaoMrd(int codVersao, [FromQuery] int? codVersaoMrd)
    {
        var r = await _scripts.ExportVersaoMrdAsync(codVersao, codVersaoMrd);
        if (r == null) return NotFound(new { message = "MRD não encontrado." });
        return File(r.Value.content, r.Value.mime, r.Value.filename);
    }

    [HttpGet("versoes/{codVersao:int}/exportar-mrd-zip")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ExportVersaoMrdZip(int codVersao)
    {
        var r = await _scripts.ExportVersaoMrdZipAsync(codVersao);
        if (r == null) return NotFound(new { message = "Nenhum MRD nesta versão." });
        return File(r.Value.content, "application/zip", r.Value.filename);
    }

    [HttpGet("versoes/anexos/{codArquivo:int}/download")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DownloadVersaoAnexo(int codArquivo)
    {
        var r = await _scripts.DownloadVersaoAnexoAsync(codArquivo);
        if (r == null) return NotFound(new { message = "Anexo não encontrado." });
        return File(r.Value.content, r.Value.mime, r.Value.filename);
    }

    [HttpDelete("versoes/anexos/{codArquivo:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteVersaoAnexo(int codArquivo)
    {
        try
        {
            await _scripts.DeleteVersaoAnexoAsync(codArquivo);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/exportar-json")]
    [Authorize]
    public async Task<IActionResult> ExportJson(int id)
    {
        var bytes = await _scripts.ExportJsonAsync(id);
        if (bytes == null) return NotFound();
        return File(bytes, "application/json", $"script_{id}.json");
    }

    [HttpGet("{id:int}/exportar-dll")]
    [Authorize]
    public async Task<IActionResult> ExportDll(int id)
    {
        var bytes = await _scripts.ExportDllAsync(id);
        if (bytes == null) return NotFound();
        return File(bytes, "application/octet-stream", $"script_{id}.dll");
    }

    [HttpGet("{id:int}/exportar-mrd")]
    [Authorize]
    public async Task<IActionResult> ExportMrd(int id, [FromQuery] int? codScriptMrd)
    {
        var r = await _scripts.ExportMrdAsync(id, codScriptMrd);
        if (r == null) return NotFound();
        return File(r.Value.content, "application/octet-stream", r.Value.filename);
    }

    [HttpGet("{id:int}/projeto-azure")]
    [Authorize]
    public async Task<IActionResult> ProjetoAzure(int id)
    {
        var url = await _scripts.GetAzureUrlAsync(id);
        if (string.IsNullOrWhiteSpace(url)) return NotFound();
        return Ok(new { success = true, url });
    }

    [HttpGet("emails-notificacao")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetEmails()
    {
        var v = await _scripts.GetEmailsNotificacaoAsync();
        return Ok(new { success = true, emails = v });
    }

    [HttpPost("emails-notificacao")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SaveEmails([FromBody] EmailsRequest req)
    {
        await _scripts.SaveEmailsNotificacaoAsync(req.Emails ?? "");
        return Ok(new { success = true });
    }

    [HttpGet("aprovar/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAprovar(string token)
    {
        var info = await _scripts.GetApprovalInfoAsync(token);
        if (info == null) return Ok(new { success = false, status = "expirado" });
        return Ok(new { success = true, data = info });
    }

    [HttpPost("aprovar/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> PostAprovar(string token, [FromBody] AprovarRequest req)
    {
        try
        {
            await _scripts.ApproveByTokenAsync(token, req.Acao == "aprovar");
            return Ok(new { success = true, acao = req.Acao });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/send-images-email")]
    [Authorize]
    public async Task<IActionResult> SendImagesEmail(int id, [FromBody] SendImagesEmailRequest req)
    {
        try
        {
            await _scripts.SendImagesEmailAsync(id, req.Email, req.Sistema);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/solicitar-aprovacao")]
    [Authorize]
    public async Task<IActionResult> SolicitarAprovacao(int id, [FromBody] SolicitarAprovacaoRequest req)
    {
        try
        {
            var sent = await _scripts.SendApprovalEmailAsync(id, req.NomeScript, req.LinkTeste, req.Emails ?? [], req.Mensagem);
            return Ok(new { success = true, emailsEnviados = sent });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string FirstUserName(string? fallback) =>
        string.IsNullOrWhiteSpace(fallback)
            ? User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin"
            : fallback;

    private static bool ParseBool(string? v) =>
        v is "true" or "on" or "1" or "True";

    private static async Task<ScriptFormInput> MapFormAsync(ScriptFormDto form)
    {
        return new ScriptFormInput
        {
            Nome = form.Nome ?? "",
            CodPacote = form.Codpacote,
            Descricao = form.Descricao,
            Linguagem = form.Linguagem,
            CaminhoProjeto = form.Caminho_projeto,
            CaminhoAzure = form.Caminho_azure,
            LinkTeste = form.Link_teste,
            Sistema = form.Sistema ?? "",
            Aprovado = ParseBool(form.Aprovado) ? 1 : 0,
            AprovadoPor = form.Aprovado_por,
            Ativo = ParseBool(form.Ativo) ? 1 : 0,
            CriadoPor = form.Criado_por ?? "",
            ArquivoJson = form.Arquivo_json != null ? await ReadBytes(form.Arquivo_json) : null,
            ArquivoDll = form.Arquivo_dll != null ? await ReadBytes(form.Arquivo_dll) : null,
            MrdFiles = await ReadFilesAsync(form.Arquivos_mrd),
            Imagens = await ReadFilesAsync(form.Imagens),
            Pdfs = await ReadFilesAsync(form.Pdfs)
        };
    }

    private static async Task<byte[]> ReadBytes(IFormFile f)
    {
        await using var ms = new MemoryStream();
        await f.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static async Task<IList<UploadedFile>?> ReadFilesAsync(IEnumerable<IFormFile>? files)
    {
        if (files == null) return null;
        var list = new List<UploadedFile>();
        foreach (var f in files.Where(x => x.Length > 0))
            list.Add(new UploadedFile { FileName = f.FileName, Content = await ReadBytes(f) });
        return list.Count > 0 ? list : null;
    }

    public class ScriptFormDto
    {
        public string? Nome { get; set; }
        public int? Codpacote { get; set; }
        public string? Descricao { get; set; }
        public string? Linguagem { get; set; }
        public string? Caminho_projeto { get; set; }
        public string? Caminho_azure { get; set; }
        public string? Link_teste { get; set; }
        public string? Sistema { get; set; }
        public string? Aprovado { get; set; }
        public string? Aprovado_por { get; set; }
        public string? Ativo { get; set; } = "true";
        public string? Criado_por { get; set; }
        public IFormFile? Arquivo_json { get; set; }
        public IFormFile? Arquivo_dll { get; set; }
        public List<IFormFile>? Arquivos_mrd { get; set; }
        public List<IFormFile>? Imagens { get; set; }
        public List<IFormFile>? Pdfs { get; set; }
    }

    public record VerificarNomeRequest(string Nome, int? ExcludeId);
    public record SaveVariaveisRequest(List<int>? CodVariaveis);
    private static async Task<VersaoFormInput> MapVersaoFormAsync(VersaoFormDto form)
    {
        var mrdExcluir = new List<int>();
        if (!string.IsNullOrWhiteSpace(form.Mrd_excluir))
        {
            foreach (var part in form.Mrd_excluir.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(part, out var id)) mrdExcluir.Add(id);
        }
        foreach (var c in form.Mrd_excluir_list ?? [])
            if (c > 0) mrdExcluir.Add(c);

        return new VersaoFormInput
        {
            NumeroVersao = form.Numero_versao ?? "",
            DescricaoAlteracoes = form.Descricao_alteracoes ?? "",
            AlteracoesInterface = form.Alteracoes_interface,
            AlteracoesCodigo = form.Alteracoes_codigo,
            Observacoes = form.Observacoes,
            UsuarioResponsavel = form.Usuario_responsavel ?? "",
            ArquivoJson = form.Arquivo_json != null ? await ReadBytes(form.Arquivo_json) : null,
            ArquivoDll = form.Arquivo_dll != null ? await ReadBytes(form.Arquivo_dll) : null,
            MrdFiles = await ReadFilesAsync(form.Arquivos_mrd),
            Imagens = await ReadFilesAsync(form.Imagens),
            Pdfs = await ReadFilesAsync(form.Pdfs),
            MrdPadraoIdx = form.Mrd_padrao_versao_idx,
            MrdPadraoCod = int.TryParse(form.Mrd_padrao, out var mp) ? mp : null,
            MrdExcluir = mrdExcluir.Count > 0 ? mrdExcluir : null
        };
    }

    public class VersaoFormDto
    {
        public string? Numero_versao { get; set; }
        public string? Descricao_alteracoes { get; set; }
        public string? Alteracoes_interface { get; set; }
        public string? Alteracoes_codigo { get; set; }
        public string? Observacoes { get; set; }
        public string? Usuario_responsavel { get; set; }
        public IFormFile? Arquivo_json { get; set; }
        public IFormFile? Arquivo_dll { get; set; }
        public List<IFormFile>? Arquivos_mrd { get; set; }
        public List<IFormFile>? Imagens { get; set; }
        public List<IFormFile>? Pdfs { get; set; }
        public int? Mrd_padrao_versao_idx { get; set; }
        public string? Mrd_padrao { get; set; }
        public string? Mrd_excluir { get; set; }
        public List<int>? Mrd_excluir_list { get; set; }
    }
    public record EmailsRequest(string? Emails);
    public record AprovarRequest(string Acao);
    public record SendImagesEmailRequest(string Email, string Sistema);
    public record SolicitarAprovacaoRequest(string NomeScript, string LinkTeste, List<string>? Emails, string? Mensagem);
}
