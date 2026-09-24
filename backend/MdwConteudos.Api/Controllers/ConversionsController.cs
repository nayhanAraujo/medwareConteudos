using ConversorHtml.Application;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Application.Services;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
using MdwConteudos.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MdwConteudos.Api.Modules.Permissions;
using MdwConteudos.Api.Modules.ApiPublica;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;

namespace MdwConteudos.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/conversions")]
public class ConversionsController : ControllerBase
{
    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/bmp", "image/gif"
    ];

    private const long MaxFileSize = 10 * 1024 * 1024;

    private readonly IImageConversionConverter _converter;
    private readonly IImageMeasureAnalyzer _analyzer;
    private readonly IConversionAnalysisService _analysisService;
    private readonly ILaudosUxHtmlValidator _htmlValidator;
    private readonly ILaudosUxModoTextoValidator _modoTextoValidator;
    private readonly ILogger<ConversionsController> _logger;

    public ConversionsController(
        IImageConversionConverter converter,
        IImageMeasureAnalyzer analyzer,
        IConversionAnalysisService analysisService,
        ILaudosUxHtmlValidator htmlValidator,
        ILaudosUxModoTextoValidator modoTextoValidator,
        ILogger<ConversionsController> logger)
    {
        _converter = converter;
        _analyzer = analyzer;
        _analysisService = analysisService;
        _htmlValidator = htmlValidator;
        _modoTextoValidator = modoTextoValidator;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    [EnableRateLimiting("studio")]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
    [RequestTimeout(300000)]
    public async Task<IActionResult> Analyze(
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var validation = ValidateImage(image);
        if (validation is not null) return validation;

        try
        {
            await using var stream = image!.OpenReadStream();
            var extracted = await _analyzer.AnalyzeAsync(stream, image.FileName, cancellationToken);
            return Ok(await _analysisService.MatchAsync(extracted, cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Autenticação Cursor falhou");
            return Unauthorized(new { message = ex.Message });
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout na análise");
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Falha na análise");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    [HttpPost("generate-modo-texto-from-analysis")]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    [EnableRateLimiting("studio")]
    public async Task<ActionResult<ConversionResponseDto>> GenerateModoTextoFromAnalysis(
        [FromBody] GenerateModoTextoFromAnalysisRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Measures.Count == 0)
        {
            return BadRequest(new { message = "Nenhuma medida informada." });
        }

        if (request.Measures.Any(m => !m.CodVariavel.HasValue
                && !string.Equals(m.Decision, "ignore", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(m.Decision, "keep", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Há medidas sem decisão de revisão." });
        }

        try
        {
            var text = await _analysisService.GenerateModoTextoAsync(request.Measures, request.CodPadraoCliente, cancellationToken);
            var validation = _modoTextoValidator.Validate(text);
            return Ok(new ConversionResponseDto
            {
                Format = ConversionOutputFormat.ModoTexto,
                Text = text,
                SourceFileName = request.SourceFileName ?? string.Empty,
                ConvertedAt = DateTime.UtcNow,
                Provider = "Analysis",
                Validation = MapValidation(validation)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("generate-json-from-analysis")]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    [EnableRateLimiting("studio")]
    public async Task<ActionResult<ConversionResponseDto>> GenerateJsonFromAnalysis(
        [FromBody] GenerateModoTextoFromAnalysisRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Measures.Count == 0)
        {
            return BadRequest(new { message = "Nenhuma medida informada." });
        }

        if (request.Measures.Any(m => !m.CodVariavel.HasValue
                && !string.Equals(m.Decision, "ignore", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(m.Decision, "keep", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Há medidas sem decisão de revisão." });
        }

        try
        {
            var text = await _analysisService.GenerateModoTextoAsync(request.Measures, request.CodPadraoCliente, cancellationToken);
            var modoTextoValidation = _modoTextoValidator.Validate(text);
            var json = ModoTextoToJsonStudioConverter.Convert(text);
            var jsonValidation = ValidateJsonStudio(json, modoTextoValidation);

            return Ok(new ConversionResponseDto
            {
                Format = ConversionOutputFormat.JsonStudio,
                Text = json,
                SourceFileName = request.SourceFileName ?? string.Empty,
                ConvertedAt = DateTime.UtcNow,
                Provider = "Analysis",
                Validation = MapValidation(jsonValidation)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("register-alternativa")]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    [RequirePermission("variaveis", "criar")]
    public async Task<IActionResult> RegisterAlternativa(
        [FromBody] RegisterAlternativaRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _analysisService.RegisterAlternativaAsync(request.CodVariavel, request.Alternativa, cancellationToken);
            return Ok(new { message = "Alternativa cadastrada." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy"
        });
    }

    [HttpGet("variables")]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    public Task<IActionResult> Variables([FromServices] IApiPublicaService variables, CancellationToken cancellationToken)
        => variables.GetVariaveisAsync(null, null, cancellationToken);

    [HttpGet("variables/{id:int}")]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    public Task<IActionResult> VariableDetails(int id, [FromServices] IApiPublicaService variables, CancellationToken cancellationToken)
        => variables.GetVariavelDetalhadaAsync(id, cancellationToken);

    [HttpPost]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    [EnableRateLimiting("studio")]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
    [RequestTimeout(300000)]
    public async Task<ActionResult<ConversionResponseDto>> Convert(
        IFormFile? image,
        [FromForm] string? format,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new { message = "Nenhuma imagem enviada." });
        }

        if (image.Length > MaxFileSize)
        {
            return BadRequest(new { message = "Imagem excede o limite de 10MB." });
        }

        if (!AllowedContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Tipo de arquivo não suportado: {image.ContentType}" });
        }

        var outputFormat = ConversionFormatParser.Parse(format);

        try
        {
            await using var stream = image.OpenReadStream();
            var content = await _converter.ConvertAsync(stream, image.FileName, outputFormat, cancellationToken);
            var validation = ValidateContent(outputFormat, content);

            var response = new ConversionResponseDto
            {
                Format = outputFormat,
                SourceFileName = image.FileName,
                ConvertedAt = DateTime.UtcNow,
                Provider = _converter.GetType().Name.Replace("ImageToHtmlConverter", "", StringComparison.Ordinal),
                Validation = MapValidation(validation)
            };

            if (outputFormat == ConversionOutputFormat.ModoTexto
                || outputFormat == ConversionOutputFormat.JsonStudio)
            {
                response.Text = content;
            }
            else
            {
                response.Html = content;
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Autenticação Cursor falhou");
            return Unauthorized(new { message = ex.Message });
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout na conversão");
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Falha na conversão");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Bridge ou recurso não encontrado");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [HttpPost("validate")]
    [RequirePermission("studio", "visualizar")]
    [RequirePermission("studio", "converter")]
    public ActionResult<ValidationResponseDto> Validate([FromBody] ValidateConversionRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "Conteúdo não informado." });
        }

        var format = ConversionFormatParser.Parse(request.Format);
        var validation = ValidateContent(format, request.Content);
        return Ok(MapValidation(validation));
    }

    private ValidationResult ValidateContent(ConversionOutputFormat format, string content) =>
        format switch
        {
            ConversionOutputFormat.ModoTexto => _modoTextoValidator.Validate(content),
            ConversionOutputFormat.JsonStudio => ValidateJsonStudio(content, null),
            _ => _htmlValidator.Validate(content)
        };

    private static ValidationResult ValidateJsonStudio(string json, ValidationResult? modoTextoValidation)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (modoTextoValidation is not null)
        {
            errors.AddRange(modoTextoValidation.Errors);
            warnings.AddRange(modoTextoValidation.Warnings.Select(w => $"[TXT] {w}"));
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("camposScript", out var campos)
                || campos.ValueKind != JsonValueKind.Array)
            {
                errors.Add("JSON Studio inválido: propriedade 'camposScript' ausente ou inválida.");
            }
            else if (campos.GetArrayLength() == 0)
            {
                warnings.Add("JSON Studio sem campos.");
            }
            else
            {
                var hasLaudo = false;
                foreach (var campo in campos.EnumerateArray())
                {
                    if (campo.ValueKind != JsonValueKind.Object) continue;
                    if (!campo.TryGetProperty("tipo", out var tipo) || tipo.ValueKind != JsonValueKind.String)
                        errors.Add("Há campo sem 'tipo' no JSON Studio.");
                    if (!campo.TryGetProperty("nome", out var nome) || nome.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(nome.GetString()))
                        errors.Add("Há campo sem 'nome' no JSON Studio.");

                    if (campo.TryGetProperty("nome", out var n)
                        && string.Equals(n.GetString(), "LAUDODESCRITIVO", StringComparison.OrdinalIgnoreCase))
                        hasLaudo = true;
                }

                if (!hasLaudo)
                    warnings.Add("JSON Studio sem campo LAUDODESCRITIVO.");
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON Studio inválido: {ex.Message}");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private IActionResult? ValidateImage(IFormFile? image)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new { message = "Nenhuma imagem enviada." });
        }

        if (image.Length > MaxFileSize)
        {
            return BadRequest(new { message = "Imagem excede o limite de 10MB." });
        }

        if (!AllowedContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Tipo de arquivo não suportado: {image.ContentType}" });
        }

        return null;
    }

    private static ValidationResponseDto MapValidation(ValidationResult validation) => new()
    {
        IsValid = validation.IsValid,
        Errors = validation.Errors,
        Warnings = validation.Warnings
    };
}
