using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MdwConteudos.Api.Infrastructure;

// Also covers errors that controllers catch and return as ObjectResult.
public sealed class ProductionErrorFilter(IHostEnvironment environment) : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!environment.IsProduction()) return;
        var status = context.Result switch
        {
            ObjectResult result => result.StatusCode,
            JsonResult result => result.StatusCode,
            _ => null
        };
        if (status is < 500 or null) return;
        context.Result = new ObjectResult(new
        {
            success = false, error = "Serviço indisponível",
            message = "Não foi possível concluir a operação. Informe o código de atendimento ao administrador.",
            traceId = context.HttpContext.TraceIdentifier
        }) { StatusCode = status };
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
