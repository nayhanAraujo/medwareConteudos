using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.Annotations;

namespace MdwConteudos.Api.Modules.ApiPublica.Swagger;

[SwaggerSchema(Description = "Resposta padrão de erro da API")]
public class ApiErrorDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

[SwaggerSchema(Description = "Envelope de listagem com sucesso")]
public class ApiListDoc<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

[SwaggerSchema(Description = "Resposta de POST /token")]
public class TokenSuccessDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("expires_info")]
    public string ExpiresInfo { get; set; } = "";
}

[SwaggerSchema(Description = "Resposta de GET /health")]
public class HealthSuccessDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("database")]
    public string Database { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

[SwaggerSchema(Description = "Resposta de GET /health quando o banco falha")]
public class HealthErrorDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("database")]
    public string Database { get; set; } = "";

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

public class VariavelItemDoc
{
    [JsonPropertyName("codvariavel")]
    public int Codvariavel { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("variavel")]
    public string Variavel { get; set; } = "";

    [JsonPropertyName("sigla")]
    public string Sigla { get; set; } = "";

    [JsonPropertyName("casas_decimais")]
    public int? CasasDecimais { get; set; }

    [JsonPropertyName("unidade_medida")]
    public string? UnidadeMedida { get; set; }

    [JsonPropertyName("especialidades")]
    public List<string> Especialidades { get; set; } = new();
}

public class VariavelDetalheDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public VariavelDetalheDataDoc Data { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

public class VariavelDetalheDataDoc
{
    [JsonPropertyName("variavel")]
    public VariavelItemDoc Variavel { get; set; } = new();

    [JsonPropertyName("normalidades")]
    public List<NormalidadeItemDoc> Normalidades { get; set; } = new();

    [JsonPropertyName("formulas")]
    public List<object> Formulas { get; set; } = new();

    [JsonPropertyName("alternativas")]
    public List<string> Alternativas { get; set; } = new();
}

public class NormalidadeItemDoc
{
    [JsonPropertyName("codnormalidade")]
    public int Codnormalidade { get; set; }

    [JsonPropertyName("sexo")]
    public string? Sexo { get; set; }

    [JsonPropertyName("valor_min")]
    public decimal? ValorMin { get; set; }

    [JsonPropertyName("valor_max")]
    public decimal? ValorMax { get; set; }

    [JsonPropertyName("referencia")]
    public ReferenciaResumoDoc? Referencia { get; set; }
}

public class ReferenciaResumoDoc
{
    [JsonPropertyName("codigo")]
    public int Codigo { get; set; }

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = "";

    [JsonPropertyName("ano")]
    public int? Ano { get; set; }
}

public class VariavelNotFoundDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("codvariavel_buscado")]
    public int CodvariavelBuscado { get; set; }
}

public class SistemaInfoDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

public class ApiMessageDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

public class ResourceNotFoundDoc
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

public class TokenRequestDoc
{
    [JsonPropertyName("senha")]
    public string Senha { get; set; } = "";
}

public class UpdateNormalidadeRequestDoc
{
    [JsonPropertyName("valor_min")]
    public decimal ValorMin { get; set; }

    [JsonPropertyName("valor_max")]
    public decimal ValorMax { get; set; }

    [JsonPropertyName("sexo")]
    public string Sexo { get; set; } = "M";

    [JsonPropertyName("idade_min")]
    public int? IdadeMin { get; set; }

    [JsonPropertyName("idade_max")]
    public int? IdadeMax { get; set; }
}
