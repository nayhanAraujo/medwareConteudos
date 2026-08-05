using ConversorHtml.Domain.Models;

namespace ConversorHtml.Application.Interfaces;

public interface ILaudosUxHtmlValidator
{
    ValidationResult Validate(string html);
}
