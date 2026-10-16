using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MdwConteudos.Api.Modules.Cadastros;

namespace MdwConteudos.Api.Modules.ApiPublica;

public partial class ApiPublicaService
{
    public async Task<IActionResult> GetPacotesAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(new CommandDefinition(@"
                SELECT CODPACOTE, NOME, DESCRICAO,
                       CASE WHEN OCTET_LENGTH(ICONE_BASE64) > 0 THEN 1 ELSE 0 END AS TEM_ICONE
                FROM PACOTES ORDER BY NOME, CODPACOTE", cancellationToken: ct));
            var list = rows.Select(r => new
            {
                codpacote = (int)r.CODPACOTE,
                nome = (string?)r.NOME,
                descricao = (string?)r.DESCRICAO,
                icone_url = Convert.ToInt32(r.TEM_ICONE) == 1 ? PacoteIconUrl((int)r.CODPACOTE) : null
            }).ToList();
            return new OkObjectResult(ApiV1Contract.Ok(list, list.Count));
        }
        catch (Exception ex) { return Err500(ex); }
    }

    public async Task<IActionResult> GetPacoteIconeAsync(int codpacote, CancellationToken ct)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync(ct);
            var icon = await conn.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "SELECT ICONE_BASE64 FROM PACOTES WHERE CODPACOTE = @codpacote",
                new { codpacote }, cancellationToken: ct));
            if (string.IsNullOrEmpty(icon))
                return new NotFoundObjectResult(new { success = false, error = "Pacote ou icone nao encontrado" });
            var validated = PacoteIcone.Validate(icon)!;
            var bytes = Convert.FromBase64String(validated[PacoteIcone.Prefix.Length..]);
            return new FileContentResult(bytes, "image/png")
            {
                EntityTag = new EntityTagHeaderValue('"' + Convert.ToHexString(SHA256.HashData(bytes)) + '"')
            };
        }
        catch (Exception ex) { return Err500(ex); }
    }

    internal static string PacoteIconUrl(int codpacote) => $"/apiconteudos/v1/pacotes/{codpacote}/icone";
}
