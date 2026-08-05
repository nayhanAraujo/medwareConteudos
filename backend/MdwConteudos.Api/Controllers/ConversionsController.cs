using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Interfaces;
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

    private readonly IImageToHtmlConverter _converter;
    private readonly ILaudosUxHtmlValidator _validator;
    private readonly ILogger<ConversionsController> _logger;

    public ConversionsController(
        IImageToHtmlConverter converter,
        ILaudosUxHtmlValidator validator,
        ILogger<ConversionsController> logger)
    {
        _converter = converter;
        _validator = validator;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            provider = _converter.GetType().Name
        });
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
    [RequestTimeout(300000)]
    public async Task<ActionResult<ConversionResponseDto>> Convert(IFormFile? image, CancellationToken cancellationToken)
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

        try
        {
            await using var stream = image.OpenReadStream();
            var html = await _converter.ConvertAsync(stream, image.FileName, cancellationToken);
            var validation = _validator.Validate(html);

            var response = new ConversionResponseDto
            {
                Html = html,
                SourceFileName = image.FileName,
                ConvertedAt = DateTime.UtcNow,
                Provider = _converter.GetType().Name.Replace("ImageToHtmlConverter", "", StringComparison.Ordinal),
                Validation = MapValidation(validation)
            };

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
    public ActionResult<ValidationResponseDto> Validate([FromBody] ValidateHtmlRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Html))
        {
            return BadRequest(new { message = "HTML não informado." });
        }

        var validation = _validator.Validate(request.Html);
        return Ok(MapValidation(validation));
    }

    private static ValidationResponseDto MapValidation(ValidationResult validation) => new()
    {
        IsValid = validation.IsValid,
        Errors = validation.Errors,
        Warnings = validation.Warnings
    };
}
