using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ShipLean.WebApi.Infrastructure;

// Keeps MVC from reading a multipart body into a form before the action runs. The action identifies the caller or checks access first, then
// streams the file part itself, so nothing is buffered or spooled to disk for a caller who is refused.
[AttributeUsage(AttributeTargets.Method)]
internal sealed class StreamedUploadAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
