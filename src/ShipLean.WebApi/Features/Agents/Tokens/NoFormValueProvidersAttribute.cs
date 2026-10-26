using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ShipLean.WebApi.Features.Agents.Tokens;

// MVC's form value providers read the form while binding, so an over-size or malformed body would fail there with the
// generic ApiError. These endpoints bind nothing: the handler reads the form itself and answers in OAuth's error format.
[AttributeUsage(AttributeTargets.Class)]
public sealed class NoFormValueProvidersAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        foreach (var factory in context.ValueProviderFactories.Where(f => f is FormValueProviderFactory or FormFileValueProviderFactory or JQueryFormValueProviderFactory).ToList())
        {
            context.ValueProviderFactories.Remove(factory);
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
