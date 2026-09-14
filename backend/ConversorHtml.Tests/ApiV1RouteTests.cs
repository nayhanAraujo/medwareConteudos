using Microsoft.AspNetCore.Mvc.Routing;
using MdwConteudos.Api.Modules.ApiPublica;

namespace ConversorHtml.Tests;

public sealed class ApiV1RouteTests
{
    // Snapshot of the 20 operations declared in legacy routes/api.py; OPTIONS are separate.
    [Fact]
    public void All_twenty_Python_operations_and_eighteen_internal_aliases_remain()
    {
        string[] shared = ["POST token", "GET health", "GET variaveis", "GET variaveis/{codvariavel:int}",
            "GET normalidades", "GET normalidades_ecodopplercardiograma", "GET normalidades/ecodopplercardiograma/{referencia:int}",
            "GET formulas", "GET referencias", "GET sistema/info", "GET especialidades", "GET relatorios",
            "GET relatorios/{codrelatorio:int}/download", "GET scripts", "GET scripts/ultimo-verificado",
            "GET scripts/{codscriptlaudo:int}/imagem", "GET scripts/{codscriptlaudo:int}/download", "PUT normalidades/{codnormalidade:int}"];
        var routes = typeof(ApiPublicaController).GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true)
            .Cast<HttpMethodAttribute>().SelectMany(a => a.HttpMethods.Select(verb => verb + " " + a.Template))).ToHashSet();
        Assert.Equal(18, shared.Length);
        foreach (var entry in shared)
        foreach (var prefix in new[] { "apiconteudos/v1/", "api/v1/" })
        {
            var parts = entry.Split(' ', 2);
            Assert.Contains(parts[0] + " " + prefix + parts[1], routes);
        }
        Assert.Contains("GET apiconteudos/v1/paineis", routes);
        Assert.Contains("GET apiconteudos/v1/paineis/{codpainel:int}/download", routes);
        Assert.Contains("OPTIONS apiconteudos/v1/formulas", routes);
        Assert.Contains("OPTIONS apiconteudos/v1/normalidades_ecodopplercardiograma", routes);
    }
}
