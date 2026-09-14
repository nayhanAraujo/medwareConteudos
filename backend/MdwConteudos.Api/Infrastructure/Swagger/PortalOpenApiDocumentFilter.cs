using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public sealed class PortalOpenApiDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        // Each definition advertises only schemes actually used by its operations.
        var used = document.Paths.Values.SelectMany(p => p.Operations?.Values.AsEnumerable() ?? [])
            .SelectMany(o => o.Security ?? []).SelectMany(r => r.Keys)
            .Select(r => r.Reference.Id).ToHashSet(StringComparer.Ordinal);
        if (document.Components?.SecuritySchemes is not null)
            foreach (var name in new[] { PartnerJwtOperationFilter.PartnerScheme, PartnerJwtOperationFilter.WebScheme })
                if (!used.Contains(name)) document.Components.SecuritySchemes.Remove(name);
    }
}
