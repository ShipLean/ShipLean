using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Mcp.Work;

// Wraps whichever IWorkItemStore the host registers, however it registers it (by type, by factory or as an instance), so a
// test can watch or interfere with the real store without knowing its implementation class.
public static class WorkItemStoreDecoration
{
    public static void Decorate(IServiceCollection services, Func<IWorkItemStore, IServiceProvider, IWorkItemStore> wrap)
    {
        var real = services.Single(d => d.ServiceType == typeof(IWorkItemStore));
        services.Remove(real);
        services.Add(new ServiceDescriptor(typeof(IWorkItemStore), sp => wrap(CreateReal(real, sp), sp), real.Lifetime));
    }

    private static IWorkItemStore CreateReal(ServiceDescriptor real, IServiceProvider sp)
        => (IWorkItemStore)(real.ImplementationInstance
                            ?? real.ImplementationFactory?.Invoke(sp)
                            ?? ActivatorUtilities.CreateInstance(sp, real.ImplementationType!));
}
