using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public class PartnerJwtOperationFilter : IOperationFilter
{
    private static readonly HashSet<string> PublicSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/token",
        "/health"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? "";
        if (!path.StartsWith("apiconteudos/v1", StringComparison.OrdinalIgnoreCase))
            return;
        if (context.ApiDescription.HttpMethod?.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase) == true)
            return;
        if (PublicSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference("PartnerJwt", null, null),
                new List<string>()
            }
        });
    }
}
