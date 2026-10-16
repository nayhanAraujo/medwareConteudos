using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MdwConteudos.Api.Modules.Cadastros;

namespace MdwConteudos.Api.Modules.ApiPublica;

public partial class ApiPublicaService
{
    public async Task<IActionResult> GetModuloRelatorioIconeAsync(int codmodulo, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var icon = await conn.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "SELECT ICONE_BASE64 FROM MODULORELATORIO WHERE CODMODULO = @codmodulo",
                new { codmodulo }, cancellationToken: ct));
            if (string.IsNullOrEmpty(icon))
                return new NotFoundObjectResult(new { success = false, error = "Modulo ou icone nao encontrado" });
            var validated = PacoteIcone.Validate(icon)!;
            var bytes = Convert.FromBase64String(validated[PacoteIcone.Prefix.Length..]);
            return new FileContentResult(bytes, "image/png")
            {
                FileDownloadName = $"modulo-{codmodulo}.png",
                EntityTag = new EntityTagHeaderValue('"' + Convert.ToHexString(SHA256.HashData(bytes)) + '"')
            };
        }
        catch (Exception ex) { return Err500(ex); }
    }
}
