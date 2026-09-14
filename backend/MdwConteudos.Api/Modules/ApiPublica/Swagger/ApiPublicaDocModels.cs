using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.Annotations;

namespace MdwConteudos.Api.Modules.ApiPublica.Swagger;

// Documentation-only contracts, never used for binding or service responses.
// Nullable permits JSON null; DocOptional permits absence.
[AttributeUsage(AttributeTargets.Property)]
public sealed class DocOptionalAttribute : Attribute;

public record ApiErrorDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("message"), DocOptional] string? Message);

public record ApiListDoc<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] List<T> Data,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("timestamp")] string Timestamp);

public record TokenSuccessDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("expires_info")] string ExpiresInfo);

public record HealthSuccessDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("timestamp")] string Timestamp);

public record HealthErrorDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("database")] string Database,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("timestamp")] string Timestamp);

public class VariavelBaseDoc
{
    [JsonPropertyName("codvariavel")] public int Codvariavel { get; set; }
    [JsonPropertyName("nome")] public string? Nome { get; set; }
    [JsonPropertyName("variavel")] public string? Variavel { get; set; }
    [JsonPropertyName("sigla")] public string? Sigla { get; set; }
    [JsonPropertyName("abreviacao")] public string? Abreviacao { get; set; }
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }
    [JsonPropertyName("casas_decimais")] public int? CasasDecimais { get; set; }
    [JsonPropertyName("unidade_medida")] public string? UnidadeMedida { get; set; }
    [JsonPropertyName("especialidades")] public List<string> Especialidades { get; set; } = [];
}
public class VariavelItemDoc : VariavelBaseDoc
{
    [JsonPropertyName("nomes_clinicos")] public List<string> NomesClinicos { get; set; } = [];
}
public record VariavelDetalheDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] VariavelDetalheDataDoc Data,
    [property: JsonPropertyName("timestamp")] string Timestamp);
public record VariavelDetalheDataDoc(
    [property: JsonPropertyName("variavel")] VariavelBaseDoc Variavel,
    [property: JsonPropertyName("normalidades")] List<NormalidadeDetalheDoc> Normalidades,
    [property: JsonPropertyName("formulas")] List<FormulaDetalheDoc> Formulas,
    [property: JsonPropertyName("alternativas")] List<string> Alternativas,
    [property: JsonPropertyName("comentarios")] List<ComentarioNormalidadeDoc> Comentarios);
public record ComentarioNormalidadeDoc(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("sexo")] string Sexo,
    [property: JsonPropertyName("idade_min")] int IdadeMin,
    [property: JsonPropertyName("idade_max")] int IdadeMax,
    [property: JsonPropertyName("texto")] string? Texto);

public class NormalidadeBaseDoc
{
    [JsonPropertyName("codnormalidade")] public int Codnormalidade { get; set; }
    [JsonPropertyName("sexo")] public string? Sexo { get; set; }
    [JsonPropertyName("valor_min")] public decimal? ValorMin { get; set; }
    [JsonPropertyName("valor_max")] public decimal? ValorMax { get; set; }
    [JsonPropertyName("idade_min")] public int? IdadeMin { get; set; }
    [JsonPropertyName("idade_max")] public int? IdadeMax { get; set; }
    [JsonPropertyName("referencia")] public ReferenciaResumoDoc? Referencia { get; set; }
}
public class NormalidadeItemDoc : NormalidadeBaseDoc
{
    [JsonPropertyName("variavel")] public VariavelResumoDoc Variavel { get; set; } = new(0, null, null);
}
public class NormalidadeDetalheDoc : NormalidadeBaseDoc
{
    [JsonPropertyName("classificacao")] public string? Classificacao { get; set; }
    [JsonPropertyName("comentario_texto")] public string? ComentarioTexto { get; set; }
}
public record VariavelResumoDoc(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("sigla")] string? Sigla);
public class ReferenciaResumoDoc
{
    [JsonPropertyName("codigo")] public int Codigo { get; set; }
    [JsonPropertyName("titulo")] public string? Titulo { get; set; }
    [JsonPropertyName("ano")] public int? Ano { get; set; }
    [JsonPropertyName("descricao")] public string? Descricao { get; set; }
    [JsonPropertyName("autores")] public string? Autores { get; set; }
}
public class ReferenciaItemDoc : ReferenciaResumoDoc
{
    [JsonPropertyName("especialidade")] public string? Especialidade { get; set; }
}
public record FormulaDetalheDoc(
    [property: JsonPropertyName("codformula")] int Codformula,
    [property: JsonPropertyName("codvariavel")] int? Codvariavel,
    [property: JsonPropertyName("formula")] string? Formula,
    [property: JsonPropertyName("casas_decimais")] int? CasasDecimais,
    [property: JsonPropertyName("equacoes")] List<EquacaoDoc> Equacoes);
public record EquacaoDoc(
    [property: JsonPropertyName("codequacao")] int? Codequacao,
    [property: JsonPropertyName("equacao")] string? Equacao,
    [property: JsonPropertyName("linguagem")] string? Linguagem,
    [property: JsonPropertyName("referencia")] ReferenciaResumoDoc? Referencia);
public record FormulaFuncaoDoc(
    [property: JsonPropertyName("nome_funcao")] string NomeFuncao,
    [property: JsonPropertyName("equacao")] string Equacao,
    [property: JsonPropertyName("linguagem")] string Linguagem);
public record VariavelNotFoundDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("codvariavel_buscado")] int CodvariavelBuscado,
    [property: JsonPropertyName("variaveis_exemplo")] List<VariavelExemploDoc> VariaveisExemplo);
// Dapper dictionary keys retain unquoted Firebird identifiers, not MVC camelCase.
public record VariavelExemploDoc(
    [property: JsonPropertyName("CODVARIAVEL")] int Codvariavel,
    [property: JsonPropertyName("NOME")] string? Nome);

public record SistemaInfoDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] SistemaInfoDataDoc Data,
    [property: JsonPropertyName("timestamp")] string Timestamp);
public record SistemaInfoDataDoc(
    [property: JsonPropertyName("estatisticas")] SistemaEstatisticasDoc Estatisticas,
    [property: JsonPropertyName("variaveis_por_especialidade")] List<VariaveisPorEspecialidadeDoc> VariaveisPorEspecialidade,
    [property: JsonPropertyName("versao_api")] string VersaoApi,
    [property: JsonPropertyName("ultima_atualizacao")] string UltimaAtualizacao);
public record SistemaEstatisticasDoc(
    [property: JsonPropertyName("total_variaveis")] int TotalVariaveis,
    [property: JsonPropertyName("total_normalidades")] int TotalNormalidades,
    [property: JsonPropertyName("total_referencias")] int TotalReferencias,
    [property: JsonPropertyName("total_autores")] int TotalAutores,
    [property: JsonPropertyName("total_especialidades")] int TotalEspecialidades,
    [property: JsonPropertyName("variaveis_com_normalidade")] int VariaveisComNormalidade,
    [property: JsonPropertyName("variaveis_com_formula")] int VariaveisComFormula);
public record VariaveisPorEspecialidadeDoc(
    [property: JsonPropertyName("especialidade")] string? Especialidade,
    [property: JsonPropertyName("total")] int Total);
public record EspecialidadeItemDoc(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("descricao")] string? Descricao,
    [property: JsonPropertyName("total_variaveis")] int TotalVariaveis);
public record RelatorioItemDoc(
    [property: JsonPropertyName("codrelatorio")] int Codrelatorio,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("modulo")] string? Modulo,
    [property: JsonPropertyName("formato")] string? Formato,
    [property: JsonPropertyName("dthrcriacao")] string? Dthrcriacao,
    [property: JsonPropertyName("ativo")] int? Ativo,
    [property: JsonPropertyName("tem_multiselecao")] bool TemMultiselecao);

public class ScriptItemDoc
{
    [JsonPropertyName("codscriptlaudo")] public int Codscriptlaudo { get; set; }
    [JsonPropertyName("nome")] public string? Nome { get; set; }
    [JsonPropertyName("descricao")] public string Descricao { get; set; } = "";
    [JsonPropertyName("linguagem")] public string Linguagem { get; set; } = "";
    [JsonPropertyName("sistema")] public string Sistema { get; set; } = "";
    [JsonPropertyName("aprovado")] public bool Aprovado { get; set; }
    [JsonPropertyName("data_verificacao")] public string? DataVerificacao { get; set; }
    [JsonPropertyName("ativo")] public bool Ativo { get; set; }
    [JsonPropertyName("aprovado_por")] public string AprovadoPor { get; set; } = "";
    [JsonPropertyName("pacote_nome")] public string PacoteNome { get; set; } = "";
    [JsonPropertyName("codpacote")] public int? Codpacote { get; set; }
    [JsonPropertyName("numero_versao"), DocOptional] public string? NumeroVersao { get; set; }
    [JsonPropertyName("imagens"), DocOptional] public List<ScriptArquivoDoc> Imagens { get; set; } = [];
    [JsonPropertyName("pdfs"), DocOptional] public List<ScriptArquivoDoc> Pdfs { get; set; } = [];
    [JsonPropertyName("mrds"), DocOptional] public List<ScriptMrdDoc> Mrds { get; set; } = [];
    [JsonPropertyName("qtd_mrd"), DocOptional] public int QtdMrd { get; set; }
    [JsonPropertyName("mrd_fonte"), DocOptional] public string MrdFonte { get; set; } = "";
}
public class ScriptVerificadoItemDoc : ScriptItemDoc
{
    [JsonPropertyName("imagem_capa"), DocOptional] public ScriptImagemCapaDoc ImagemCapa { get; set; } = new(0, "", "");
}
public record ScriptArquivoDoc(
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("caminho")] string Caminho);
public record ScriptMrdDoc(
    [property: JsonPropertyName("codscriptmrd")] int? Codscriptmrd,
    [property: JsonPropertyName("codversaomrd")] int? Codversaomrd,
    [property: JsonPropertyName("nome_arquivo")] string NomeArquivo,
    [property: JsonPropertyName("padrao")] bool Padrao,
    [property: JsonPropertyName("ordem")] int? Ordem);
public record ScriptImagemCapaDoc(
    [property: JsonPropertyName("indice")] int Indice,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("caminho_relativo_api")] string CaminhoRelativoApi);
public record ScriptUltimoVerificadoDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] ScriptVerificadoItemDoc Data,
    [property: JsonPropertyName("workflow_key")] string? WorkflowKey,
    [property: JsonPropertyName("timestamp")] string Timestamp);
public record PainelItemDoc(
    [property: JsonPropertyName("codpainel")] int Codpainel,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("descricao")] string? Descricao,
    [property: JsonPropertyName("ativo")] int? Ativo,
    [property: JsonPropertyName("tipo_painel")] string? TipoPainel,
    [property: JsonPropertyName("codcliente")] int? Codcliente,
    [property: JsonPropertyName("nome_cliente")] string? NomeCliente,
    [property: JsonPropertyName("codmodulo")] int? Codmodulo,
    [property: JsonPropertyName("nome_modulo")] string? NomeModulo,
    [property: JsonPropertyName("tem_arquivo_pbix")] bool TemArquivoPbix,
    [property: JsonPropertyName("nome_arquivo_pbix")] string? NomeArquivoPbix,
    [property: JsonPropertyName("pacotes")] List<string> Pacotes);

public record NormalidadeFaixaDoc(
    [property: JsonPropertyName("min")] double? Min,
    [property: JsonPropertyName("max")] double? Max,
    [property: JsonPropertyName("_meta")] NormalidadeMetaDoc Meta);
public record NormalidadeMetaDoc(
    [property: JsonPropertyName("Pagina"), DocOptional] double Pagina,
    [property: JsonPropertyName("Fonte"), DocOptional] string Fonte,
    [property: JsonPropertyName("Ano"), DocOptional] int Ano);
// ApiPublicaSchemaFilter supplies typed additionalProperties for dynamic sex keys.
public class ClienteVariavelDoc
{
    [JsonPropertyName("_comentario_texto"), DocOptional]
    public Dictionary<string, string> ComentarioTexto { get; set; } = [];
}
public record ClienteCodigoDoc([property: JsonPropertyName("codigo")] int Codigo);
public record ClientePadraoDoc(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("codigo_api")] string CodigoApi,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("vigente")] bool Vigente);
public record ClienteNormalidadesDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("cliente")] ClienteCodigoDoc Cliente,
    [property: JsonPropertyName("padrao")] ClientePadraoDoc Padrao,
    [property: JsonPropertyName("data")] Dictionary<string, ClienteVariavelDoc> Data);
public record ClientePadraoItemDoc(
    [property: JsonPropertyName("codigo")] int Codigo,
    [property: JsonPropertyName("codigo_api")] string CodigoApi,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("cod_referencia_origem")] int? CodReferenciaOrigem,
    [property: JsonPropertyName("padrao_vigente")] bool PadraoVigente,
    [property: JsonPropertyName("descricao")] string? Descricao);

public record ApiMessageDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("timestamp")] string Timestamp);
public record ResourceNotFoundDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string Error);
public record RelatorioNotFoundDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("codrelatorio")] int Codrelatorio);
public record PainelNotFoundDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("codpainel")] int Codpainel);
public record ScriptErrorDoc(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("codscriptlaudo")] int Codscriptlaudo,
    [property: JsonPropertyName("sistema"), DocOptional] string? Sistema,
    [property: JsonPropertyName("indice"), DocOptional] int Indice,
    [property: JsonPropertyName("meta"), DocOptional] ScriptArquivoDoc? Meta);
