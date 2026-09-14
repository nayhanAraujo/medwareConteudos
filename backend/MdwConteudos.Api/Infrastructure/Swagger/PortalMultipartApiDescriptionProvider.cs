using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MdwConteudos.Api.Infrastructure.Swagger;

/// <summary>Normalizes ApiExplorer's file metadata only; never changes MVC action binding.</summary>
public sealed class PortalMultipartApiDescriptionProvider(IModelMetadataProvider metadata) : IApiDescriptionProvider
{
    public int Order => -2000;
    public void OnProvidersExecuting(ApiDescriptionProviderContext context) { }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
        foreach (var api in context.Results)
        foreach (var group in api.ParameterDescriptions.Where(p => p.ParameterDescriptor?.ParameterType == typeof(IFormFile))
                     .GroupBy(p => p.ParameterDescriptor!.Name).ToArray())
        {
            var original = group.First().ParameterDescriptor!;
            foreach (var expanded in group.ToArray()) api.ParameterDescriptions.Remove(expanded);
            var name = original.BindingInfo?.BinderModelName ?? original.Name;
            // Recombine ApiExplorer's expanded IFormFile properties into one binary part.
            // Only documentation changes: MVC still binds the original action descriptor.
            api.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = name, Type = typeof(IFormFile), Source = BindingSource.FormFile,
                ModelMetadata = metadata.GetMetadataForType(typeof(IFormFile)),
                ParameterDescriptor = new ParameterDescriptor
                {
                    Name = name, ParameterType = typeof(IFormFile),
                    BindingInfo = new BindingInfo { BindingSource = BindingSource.FormFile }
                }
            });
        }
    }
}
