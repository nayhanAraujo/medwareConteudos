using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MdwConteudos.Api.Modules.ApiPublica;

namespace MdwConteudos.Api.Modules.Documentation;

public sealed class LegacyApiValidationFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Controller is not ApiPublicaController) return;
        if (context.Result is UnsupportedMediaTypeResult
            || context.Result is ObjectResult { StatusCode: 415 })
            context.Result = new BadRequestObjectResult(new
            {
                success = false,
                error = "Content-Type application/json é obrigatório",
                message = "Envie um corpo JSON válido."
            });
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
