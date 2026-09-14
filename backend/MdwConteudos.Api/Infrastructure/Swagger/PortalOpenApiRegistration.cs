using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MdwConteudos.Api.Infrastructure.Swagger;

public static class PortalOpenApiRegistration
{
    /// <summary>Register alongside AddControllers. Does not initialize the application or database.</summary>
    public static IServiceCollection AddPortalOpenApiMetadata(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IApiDescriptionProvider, PortalMultipartApiDescriptionProvider>());
        return services;
    }

    /// <summary>Call inside AddSwaggerGen; document titles, versions and inclusion remain Main's responsibility.</summary>
    public static SwaggerGenOptions AddPortalOpenApi(this SwaggerGenOptions options)
    {
        options.SupportNonNullableReferenceTypes();
        options.CustomSchemaIds(SchemaId);
        options.AddSecurityDefinition(PartnerJwtOperationFilter.PartnerScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            In = ParameterLocation.Header, Name = "Authorization",
            Description = "JWT de parceiro, obtido em POST /apiconteudos/v1/token. Informe somente o token; o cliente acrescenta Bearer."
        });
        options.AddSecurityDefinition(PartnerJwtOperationFilter.WebScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            In = ParameterLocation.Header, Name = "Authorization",
            Description = "JWT de login web, obtido em POST /api/web/auth/login. Não aceita JWT de parceiros. Informe somente o token."
        });
        options.SchemaFilter<ApiPublicaSchemaFilter>();
        options.OperationFilter<PortalOpenApiOperationFilter>();
        options.OperationFilter<PartnerJwtOperationFilter>();
        options.OperationFilter<ApiPublicaExamplesOperationFilter>();
        options.DocumentFilter<PortalOpenApiDocumentFilter>();
        return options;
    }

    private static string SchemaId(Type type)
    {
        if (type.IsArray) return SchemaId(type.GetElementType()!) + "Array";
        var name = type.Namespace == typeof(MdwConteudos.Api.Modules.ApiPublica.Swagger.ApiErrorDoc).Namespace
            ? type.Name : (type.FullName ?? type.Name).Split('[')[0].Replace('+', '.');
        if (!type.IsGenericType) return name;
        return name.Split('`')[0] + "Of" + string.Join("And", type.GetGenericArguments().Select(SchemaId));
    }
}
