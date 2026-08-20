using ConversorHtml.Application;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Interfaces;
using ConversorHtml.Domain.Enums;
using ConversorHtml.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace MdwConteudos.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/conversions")]
public class ConversionsController : ControllerBase
{
    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/bmp", "image/gif"
    ];

    private const long MaxFileSize = 10 * 1024 * 1024;

    private readonly IImageConversionConverter _converter;
    private readonly ILaudosUxHtmlValidator _htmlValidator;
    private readonly ILaudosUxModoTextoValidator _modoTextoValidator;
    private readonly ILogger<ConversionsController> _logger;

    public ConversionsController(
        IImageConversionConverter converter,
        ILaudosUxHtmlValidator htmlValidator,
        ILaudosUxModoTextoValidator modoTextoValidator,
        ILogger<ConversionsController> logger)
    {
        _converter = converter;
        _htmlValidator = htmlValidator;
        _modoTextoValidator = modoTextoValidator;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            provider = _converter.GetType().Name,
            formats = new[] { "html", "modoTexto" }
        });
    }

    [HttpPost]
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

            if (outputFormat == ConversionOutputFormat.ModoTexto)
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
        format == ConversionOutputFormat.ModoTexto
            ? _modoTextoValidator.Validate(content)
            : _htmlValidator.Validate(content);

    private static ValidationResponseDto MapValidation(ValidationResult validation) => new()
    {
        IsValid = validation.IsValid,
        Errors = validation.Errors,
        Warnings = validation.Warnings
    };
}
