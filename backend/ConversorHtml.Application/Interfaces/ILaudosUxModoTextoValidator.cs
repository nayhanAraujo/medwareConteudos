using ConversorHtml.Domain.Models;

namespace ConversorHtml.Application.Interfaces;

public interface ILaudosUxModoTextoValidator
{
    ValidationResult Validate(string text);
}
