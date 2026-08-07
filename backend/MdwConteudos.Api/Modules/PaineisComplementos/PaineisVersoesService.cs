using System.Data.Common;
using System.Text;
using System.Text.Json;
using Dapper;
using MdwConteudos.Api.Infrastructure;
using MdwConteudos.Api.Modules.ApiPublica;

namespace MdwConteudos.Api.Modules.PaineisComplementos;

public interface IPaineisVersoesService
{
    Task<IReadOnlyList<object>> ListAsync(int painelId, CancellationToken ct);
    Task<object?> GetAsync(int painelId, int versaoId, CancellationToken ct);
    Task<int> CreateAsync(int painelId, VersaoPainelForm form, int codUsuario, string responsavel, CancellationToken ct);
    Task UpdateAsync(int painelId, int versaoId, VersaoPainelForm form, string responsavel, CancellationToken ct);
    Task DeleteAsync(int painelId, int versaoId, CancellationToken ct);
    Task<ArquivoVersao> DownloadAsync(int painelId, int versaoId, string tipo, int? imagemId, CancellationToken ct);
    Task DeleteImageAsync(int painelId, int versaoId, int imagemId, CancellationToken ct);
    Task AddMetricaAsync(int painelId, int versaoId, MetricaRequest request, CancellationToken ct);
    Task AddDimensaoAsync(int painelId, int versaoId, DimensaoRequest request, CancellationToken ct);
    Task AddFonteAsync(int painelId, int versaoId, FonteDadosRequest request, CancellationToken ct);
}

public sealed class PaineisVersoesService(IFirebirdConnectionFactory db) : IPaineisVersoesService
{
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public async Task<IReadOnlyList<object>> ListAsync(int painelId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsurePainel(conn, painelId);
        var rows = await conn.QueryAsync(@"
            SELECT v.CODVERSAOPAINEL AS CodVersaoPainel, v.NUMEROVERSAO AS NumeroVersao,
                   v.DATACRIACAO AS DataCriacao, v.PUBLICADO AS Publicado, v.OBSERVACOES AS Observacoes,
                   (SELECT COUNT(*) FROM IMAGENSPAINEL i WHERE i.CODVERSAOPAINEL=v.CODVERSAOPAINEL) AS TotalImagens,
                   (SELECT COUNT(*) FROM ARQUIVOSVERSOES a WHERE a.CODVERSAOPAINEL=v.CODVERSAOPAINEL) AS TotalArquivos
            FROM VersoesPainel v WHERE v.CODPAINEL=@painelId
            ORDER BY COALESCE(v.DATACRIACAO, CURRENT_TIMESTAMP) DESC, v.CODVERSAOPAINEL DESC", new { painelId });
        return rows.Cast<object>().ToList();
    }

    public async Task<object?> GetAsync(int painelId, int versaoId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var versao = await conn.QueryFirstOrDefaultAsync(@"
            SELECT v.CODVERSAOPAINEL AS CodVersaoPainel, v.CODPAINEL AS CodPainel,
                   v.NUMEROVERSAO AS NumeroVersao, v.DATACRIACAO AS DataCriacao,
                   v.PUBLICADO AS Publicado, v.OBSERVACOES AS Observacoes,
                   p.NOME AS NomePainel, p.TIPO_PAINEL AS TipoPainel
            FROM VersoesPainel v JOIN Paineis p ON p.CODPAINEL=v.CODPAINEL
            WHERE v.CODPAINEL=@painelId AND v.CODVERSAOPAINEL=@versaoId", new { painelId, versaoId });
        if (versao is null) return null;

        object? configuracao = string.Equals((string?)versao.TipoPainel, "POWERBI", StringComparison.OrdinalIgnoreCase)
            ? await conn.QueryFirstOrDefaultAsync(@"
                SELECT CODCONFIGPOWERBI AS CodConfiguracao, DIRETORIO_PBIX AS DiretorioPbix,
                       NOME_ARQUIVO_PBIX AS NomeArquivoPbix, WORKSPACE_POWERBI AS WorkspacePowerbi,
                       DATASET_POWERBI AS DatasetPowerbi, GATEWAY_POWERBI AS GatewayPowerbi,
                       FREQUENCIA_ATUALIZACAO AS FrequenciaAtualizacao,
                       RESPONSAVEL_ATUALIZACAO AS ResponsavelAtualizacao, PUBLICLINK AS PublicLink,
                       IFRAMECODE AS IframeCode, OBSERVACOES AS Observacoes
                FROM CONFIGURACOESPOWERBI WHERE CODVERSAOPAINEL=@versaoId", new { versaoId })
            : await conn.QueryFirstOrDefaultAsync(@"
                SELECT CODCONFIGAPI AS CodConfiguracao, ENDERECO_API AS EnderecoApi,
                       RESPONSAVEL_API AS ResponsavelApi, OBSERVACOES AS Observacoes,
                       CASE WHEN ARQUIVO_JSON IS NULL THEN 0 ELSE 1 END AS TemJson
                FROM CONFIGURACOESAPI WHERE CODVERSAOPAINEL=@versaoId", new { versaoId });

        var imagens = (await conn.QueryAsync(@"
            SELECT CODIMAGEMPAINEL AS CodImagemPainel, NOME_ARQUIVO AS NomeArquivo,
                   TAMANHO_ARQUIVO AS TamanhoArquivo, TIPO_IMAGEM AS TipoImagem,
                   ORDEM_EXIBICAO AS OrdemExibicao
            FROM IMAGENSPAINEL WHERE CODVERSAOPAINEL=@versaoId ORDER BY ORDEM_EXIBICAO", new { versaoId })).ToList();
        var arquivos = (await conn.QueryAsync(@"
            SELECT CODARQUIVOVERSAO AS CodArquivoVersao, TIPO_ARQUIVO AS TipoArquivo,
                   NOME_ARQUIVO AS NomeArquivo, TAMANHO_ARQUIVO AS TamanhoArquivo,
                   MIME_TYPE AS MimeType, DATACRIACAO AS DataCriacao
            FROM ARQUIVOSVERSOES WHERE CODVERSAOPAINEL=@versaoId ORDER BY TIPO_ARQUIVO", new { versaoId })).ToList();
        var metricas = (await conn.QueryAsync(@"
            SELECT CODMETRICA AS CodMetrica, NOME AS Nome, UNIDADEMEDIDA AS UnidadeMedida,
                   CONCEITO AS Conceito, MEDIDADAX AS MedidaDax
            FROM Metricas WHERE CODVERSAOPAINEL=@versaoId ORDER BY NOME", new { versaoId })).ToList();
        var dimensoes = (await conn.QueryAsync(@"
            SELECT CODDIMENSAO AS CodDimensao, NOME AS Nome, DESCRICAO AS Descricao,
                   DOMINIOHIERARQUIA AS DominioHierarquia
            FROM Dimensoes WHERE CODVERSAOPAINEL=@versaoId ORDER BY NOME", new { versaoId })).ToList();
        var fontes = (await conn.QueryAsync(@"
            SELECT CODFONTEDADOS AS CodFonteDados, ORIGEM AS Origem, METODOEXTRACAO AS MetodoExtracao,
                   ARQUIVO AS Arquivo, PERIODOATUALIZACAO AS PeriodoAtualizacao, RESPONSAVEL AS Responsavel
            FROM FontesDados WHERE CODVERSAOPAINEL=@versaoId ORDER BY ORIGEM", new { versaoId })).ToList();
        return new { versao, configuracao, imagens, arquivos, metricas, dimensoes, fontes };
    }

    public async Task<int> CreateAsync(int painelId, VersaoPainelForm form, int codUsuario, string responsavel, CancellationToken ct)
    {
        Validate(form);
        await using var conn = await db.OpenConnectionAsync(ct);
        var tipo = await GetTipoPainel(conn, painelId);
        await EnsureUniqueNumber(conn, painelId, form.NumeroVersao, null);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO VersoesPainel
                    (CODPAINEL, NUMEROVERSAO, DATACRIACAO, PUBLICADO, OBSERVACOES, CODUSUARIO_CRIACAO)
                VALUES (@painelId, @numero, CURRENT_TIMESTAMP, @publicado, @observacoes, @codUsuario)
                RETURNING CODVERSAOPAINEL", new { painelId, numero = form.NumeroVersao.Trim(), publicado = Flag(form.Publicado), observacoes = Clean(form.Observacoes), codUsuario }, tx);
            await SaveConfiguration(conn, tx, id, tipo, form, false, ct);
            await SaveFiles(conn, tx, id, form, false, ct);
            await Log(conn, tx, id, "CRIACAO", $"Criação da versão {form.NumeroVersao.Trim()}", responsavel);
            await tx.CommitAsync(ct);
            return id;
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task UpdateAsync(int painelId, int versaoId, VersaoPainelForm form, string responsavel, CancellationToken ct)
    {
        Validate(form);
        await using var conn = await db.OpenConnectionAsync(ct);
        var tipo = await GetTipoPainel(conn, painelId);
        await EnsureVersion(conn, painelId, versaoId);
        await EnsureUniqueNumber(conn, painelId, form.NumeroVersao, versaoId);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await conn.ExecuteAsync(@"
                UPDATE VersoesPainel SET NUMEROVERSAO=@numero, PUBLICADO=@publicado, OBSERVACOES=@observacoes
                WHERE CODPAINEL=@painelId AND CODVERSAOPAINEL=@versaoId",
                new { numero = form.NumeroVersao.Trim(), publicado = Flag(form.Publicado), observacoes = Clean(form.Observacoes), painelId, versaoId }, tx);
            await SaveConfiguration(conn, tx, versaoId, tipo, form, true, ct);
            await SaveFiles(conn, tx, versaoId, form, true, ct);
            await Log(conn, tx, versaoId, "EDICAO", $"Edição da versão {form.NumeroVersao.Trim()}", responsavel);
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task DeleteAsync(int painelId, int versaoId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureVersion(conn, painelId, versaoId);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            foreach (var table in new[] { "Metricas", "Dimensoes", "FontesDados", "LOGALTERACOES", "CONFIGURACOESPOWERBI", "CONFIGURACOESAPI", "ARQUIVOSVERSOES", "IMAGENSPAINEL" })
                await conn.ExecuteAsync($"DELETE FROM {table} WHERE CODVERSAOPAINEL=@versaoId", new { versaoId }, tx);
            await conn.ExecuteAsync("DELETE FROM VersoesPainel WHERE CODPAINEL=@painelId AND CODVERSAOPAINEL=@versaoId", new { painelId, versaoId }, tx);
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<ArquivoVersao> DownloadAsync(int painelId, int versaoId, string tipo, int? imagemId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureVersion(conn, painelId, versaoId);
        tipo = tipo.Trim().ToUpperInvariant();
        if (tipo == "IMAGEM")
        {
            var row = await conn.QueryFirstOrDefaultAsync(@"
                SELECT IMAGEM, NOME_ARQUIVO FROM IMAGENSPAINEL
                WHERE CODVERSAOPAINEL=@versaoId AND (@imagemId IS NULL OR CODIMAGEMPAINEL=@imagemId)
                ORDER BY ORDEM_EXIBICAO", new { versaoId, imagemId });
            if (row is null) throw new KeyNotFoundException("Imagem não encontrada.");
            var stored = BlobHelper.ToBytes((object?)row.IMAGEM) ?? [];
            byte[] bytes;
            try { bytes = Convert.FromBase64String(Encoding.UTF8.GetString(stored)); }
            catch (FormatException) { bytes = stored; }
            var name = SafeName((string?)row.NOME_ARQUIVO, $"imagem_{versaoId}.jpg");
            return new(bytes, name, ImageMime(name));
        }
        if (tipo == "JSON")
        {
            var row = await conn.QueryFirstOrDefaultAsync(@"
                SELECT c.ARQUIVO_JSON, p.NOME, v.NUMEROVERSAO
                FROM CONFIGURACOESAPI c JOIN VersoesPainel v ON v.CODVERSAOPAINEL=c.CODVERSAOPAINEL
                JOIN Paineis p ON p.CODPAINEL=v.CODPAINEL WHERE c.CODVERSAOPAINEL=@versaoId", new { versaoId });
            var bytes = row is null ? null : BlobHelper.ToBytes((object?)row.ARQUIVO_JSON);
            if (bytes is null or { Length: 0 }) throw new KeyNotFoundException("Arquivo JSON não encontrado.");
            return new(bytes, SafeName($"{row!.NOME}_versao_{row.NUMEROVERSAO}.json", $"painel_{versaoId}.json"), "application/json");
        }
        if (tipo is not ("MANUAL" or "INSIGHTS")) throw new InvalidOperationException("Tipo de download inválido.");
        var file = await conn.QueryFirstOrDefaultAsync(@"
            SELECT ARQUIVO, NOME_ARQUIVO, MIME_TYPE FROM ARQUIVOSVERSOES
            WHERE CODVERSAOPAINEL=@versaoId AND TIPO_ARQUIVO=@tipo", new { versaoId, tipo });
        var content = file is null ? null : BlobHelper.ToBytes((object?)file.ARQUIVO);
        if (content is null or { Length: 0 }) throw new KeyNotFoundException("Arquivo não encontrado.");
        return new(content, SafeName((string?)file!.NOME_ARQUIVO, $"{tipo.ToLowerInvariant()}_{versaoId}.docx"), (string?)file.MIME_TYPE ?? DocxMime);
    }

    public async Task DeleteImageAsync(int painelId, int versaoId, int imagemId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureVersion(conn, painelId, versaoId);
        var count = await conn.ExecuteAsync("DELETE FROM IMAGENSPAINEL WHERE CODVERSAOPAINEL=@versaoId AND CODIMAGEMPAINEL=@imagemId", new { versaoId, imagemId });
        if (count == 0) throw new KeyNotFoundException("Imagem não encontrada.");
    }

    public Task AddMetricaAsync(int painelId, int versaoId, MetricaRequest r, CancellationToken ct) => AddChild(painelId, versaoId, r.Nome,
        "INSERT INTO Metricas (CODVERSAOPAINEL,NOME,UNIDADEMEDIDA,CONCEITO,MEDIDADAX) VALUES (@versaoId,@Nome,@UnidadeMedida,@Conceito,@MedidaDax)", r, ct);
    public Task AddDimensaoAsync(int painelId, int versaoId, DimensaoRequest r, CancellationToken ct) => AddChild(painelId, versaoId, r.Nome,
        "INSERT INTO Dimensoes (CODVERSAOPAINEL,NOME,DESCRICAO,DOMINIOHIERARQUIA) VALUES (@versaoId,@Nome,@Descricao,@DominioHierarquia)", r, ct);
    public Task AddFonteAsync(int painelId, int versaoId, FonteDadosRequest r, CancellationToken ct) => AddChild(painelId, versaoId, r.Origem,
        "INSERT INTO FontesDados (CODVERSAOPAINEL,ORIGEM,METODOEXTRACAO,ARQUIVO,PERIODOATUALIZACAO,RESPONSAVEL) VALUES (@versaoId,@Origem,@MetodoExtracao,@Arquivo,@PeriodoAtualizacao,@Responsavel)", r, ct);

    private async Task AddChild(int painelId, int versaoId, string required, string sql, object request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(required)) throw new InvalidOperationException("O nome/origem é obrigatório.");
        await using var conn = await db.OpenConnectionAsync(ct);
        await EnsureVersion(conn, painelId, versaoId);
        var p = new DynamicParameters(request); p.Add("versaoId", versaoId);
        await conn.ExecuteAsync(sql, p);
    }

    private static async Task SaveConfiguration(DbConnection conn, DbTransaction tx, int id, string tipo, VersaoPainelForm form, bool update, CancellationToken ct)
    {
        if (tipo == "POWERBI")
        {
            var exists = update && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CONFIGURACOESPOWERBI WHERE CODVERSAOPAINEL=@id", new { id }, tx) > 0;
            var args = new { id, dir=Clean(form.DiretorioPbix), file=Clean(form.NomeArquivoPbix), workspace=Clean(form.WorkspacePowerbi), dataset=Clean(form.DatasetPowerbi), gateway=Clean(form.GatewayPowerbi), frequency=Clean(form.FrequenciaAtualizacao), owner=Clean(form.ResponsavelAtualizacao), link=Clean(form.PublicLink), iframe=Clean(form.IframeCode), notes=Clean(form.Observacoes) };
            if (exists) await conn.ExecuteAsync(@"UPDATE CONFIGURACOESPOWERBI SET DIRETORIO_PBIX=@dir,NOME_ARQUIVO_PBIX=@file,WORKSPACE_POWERBI=@workspace,DATASET_POWERBI=@dataset,GATEWAY_POWERBI=@gateway,FREQUENCIA_ATUALIZACAO=@frequency,RESPONSAVEL_ATUALIZACAO=@owner,PUBLICLINK=@link,IFRAMECODE=@iframe,OBSERVACOES=@notes WHERE CODVERSAOPAINEL=@id", args, tx);
            else await conn.ExecuteAsync(@"INSERT INTO CONFIGURACOESPOWERBI (CODVERSAOPAINEL,DIRETORIO_PBIX,NOME_ARQUIVO_PBIX,WORKSPACE_POWERBI,DATASET_POWERBI,GATEWAY_POWERBI,FREQUENCIA_ATUALIZACAO,RESPONSAVEL_ATUALIZACAO,PUBLICLINK,IFRAMECODE,OBSERVACOES,DATACRIACAO) VALUES (@id,@dir,@file,@workspace,@dataset,@gateway,@frequency,@owner,@link,@iframe,@notes,CURRENT_TIMESTAMP)", args, tx);
        }
        else
        {
            var json = form.JsonApi is null ? null : await ReadJson(form.JsonApi, ct);
            var exists = update && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM CONFIGURACOESAPI WHERE CODVERSAOPAINEL=@id", new { id }, tx) > 0;
            var args = new { id, address=Clean(form.EnderecoApi), owner=Clean(form.ResponsavelApi), json, notes=Clean(form.Observacoes) };
            if (exists)
            {
                var jsonSet = json is null ? "" : ",ARQUIVO_JSON=@json";
                await conn.ExecuteAsync($"UPDATE CONFIGURACOESAPI SET ENDERECO_API=@address,RESPONSAVEL_API=@owner,OBSERVACOES=@notes{jsonSet} WHERE CODVERSAOPAINEL=@id", args, tx);
            }
            else await conn.ExecuteAsync(@"INSERT INTO CONFIGURACOESAPI (CODVERSAOPAINEL,ENDERECO_API,RESPONSAVEL_API,ARQUIVO_JSON,OBSERVACOES,DATACRIACAO) VALUES (@id,@address,@owner,@json,@notes,CURRENT_TIMESTAMP)", args, tx);
        }
    }

    private static async Task SaveFiles(DbConnection conn, DbTransaction tx, int id, VersaoPainelForm form, bool update, CancellationToken ct)
    {
        await SaveDocument(conn, tx, id, "MANUAL", form.ManualDocx, update, ct);
        await SaveDocument(conn, tx, id, "INSIGHTS", form.InsightsDocx, update, ct);
        var order = (await conn.ExecuteScalarAsync<int?>("SELECT MAX(ORDEM_EXIBICAO) FROM IMAGENSPAINEL WHERE CODVERSAOPAINEL=@id", new { id }, tx) ?? 0) + 1;
        foreach (var image in form.Imagens.Where(x => x.Length > 0))
        {
            if (!IsImage(image.FileName)) throw new InvalidOperationException($"Formato de imagem inválido: {image.FileName}");
            var bytes = await ReadFile(image, ct);
            var base64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes));
            await conn.ExecuteAsync(@"INSERT INTO IMAGENSPAINEL (CODVERSAOPAINEL,IMAGEM,NOME_ARQUIVO,TAMANHO_ARQUIVO,TIPO_IMAGEM,ORDEM_EXIBICAO,DATACRIACAO) VALUES (@id,@base64,@name,@size,'SCREENSHOT',@order,CURRENT_TIMESTAMP)", new { id, base64, name=SafeName(image.FileName,"imagem"), size=bytes.Length, order }, tx);
            order++;
        }
    }

    private static async Task SaveDocument(DbConnection conn, DbTransaction tx, int id, string type, IFormFile? file, bool update, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return;
        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"{type}: apenas arquivos .docx são aceitos.");
        var bytes = await ReadFile(file, ct);
        var exists = update && await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ARQUIVOSVERSOES WHERE CODVERSAOPAINEL=@id AND TIPO_ARQUIVO=@type", new { id, type }, tx) > 0;
        var args = new { id, type, name=SafeName(file.FileName,$"{type.ToLowerInvariant()}.docx"), bytes, size=bytes.Length, mime=DocxMime };
        if (exists) await conn.ExecuteAsync("UPDATE ARQUIVOSVERSOES SET NOME_ARQUIVO=@name,ARQUIVO=@bytes,TAMANHO_ARQUIVO=@size,MIME_TYPE=@mime WHERE CODVERSAOPAINEL=@id AND TIPO_ARQUIVO=@type", args, tx);
        else await conn.ExecuteAsync("INSERT INTO ARQUIVOSVERSOES (CODVERSAOPAINEL,TIPO_ARQUIVO,NOME_ARQUIVO,ARQUIVO,TAMANHO_ARQUIVO,MIME_TYPE,DATACRIACAO) VALUES (@id,@type,@name,@bytes,@size,@mime,CURRENT_TIMESTAMP)", args, tx);
    }

    private static async Task<byte[]> ReadJson(IFormFile file, CancellationToken ct)
    {
        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("O arquivo da API deve ser JSON.");
        var bytes = await ReadFile(file, ct);
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        try { using var _ = JsonDocument.Parse(text); }
        catch (JsonException ex) { throw new InvalidOperationException($"JSON inválido: {ex.Message}"); }
        return Encoding.UTF8.GetBytes(text);
    }

    private static async Task<byte[]> ReadFile(IFormFile file, CancellationToken ct) { await using var ms = new MemoryStream(); await file.CopyToAsync(ms, ct); return ms.ToArray(); }
    private static async Task Log(DbConnection conn, DbTransaction tx, int id, string type, string description, string responsible) => await conn.ExecuteAsync("INSERT INTO LOGALTERACOES (CODVERSAOPAINEL,DATAALTERACAO,TIPOALTERACAO,DESCRICAOALTERACAO,RESPONSAVEL) VALUES (@id,CURRENT_TIMESTAMP,@type,@description,@responsible)", new { id, type, description, responsible }, tx);
    private static void Validate(VersaoPainelForm form) { if (string.IsNullOrWhiteSpace(form.NumeroVersao)) throw new InvalidOperationException("Número da versão é obrigatório."); }
    private static int Flag(bool value) => value ? 1 : 0;
    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
    private static bool IsImage(string name) => new[] { ".jpg", ".jpeg", ".png", ".gif" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
    private static string ImageMime(string name) => Path.GetExtension(name).ToLowerInvariant() switch { ".png" => "image/png", ".gif" => "image/gif", _ => "image/jpeg" };
    private static string SafeName(string? value, string fallback) { var name=Path.GetFileName(value); return string.IsNullOrWhiteSpace(name) ? fallback : name; }
    private static async Task EnsurePainel(DbConnection conn, int id) { if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Paineis WHERE CODPAINEL=@id", new { id }) == 0) throw new KeyNotFoundException("Painel não encontrado."); }
    private static async Task<string> GetTipoPainel(DbConnection conn, int id) { var type=await conn.QueryFirstOrDefaultAsync<string>("SELECT TIPO_PAINEL FROM Paineis WHERE CODPAINEL=@id", new { id }); return type?.ToUpperInvariant() is "POWERBI" or "API" ? type.ToUpperInvariant() : throw new KeyNotFoundException("Painel não encontrado ou tipo inválido."); }
    private static async Task EnsureVersion(DbConnection conn, int painelId, int versaoId) { if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VersoesPainel WHERE CODPAINEL=@painelId AND CODVERSAOPAINEL=@versaoId", new { painelId, versaoId }) == 0) throw new KeyNotFoundException("Versão não encontrada."); }
    private static async Task EnsureUniqueNumber(DbConnection conn, int painelId, string numero, int? exceptId) { if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VersoesPainel WHERE CODPAINEL=@painelId AND NUMEROVERSAO=@numero AND (@exceptId IS NULL OR CODVERSAOPAINEL<>@exceptId)", new { painelId, numero=numero.Trim(), exceptId }) > 0) throw new InvalidOperationException("Esta versão já existe."); }
}
