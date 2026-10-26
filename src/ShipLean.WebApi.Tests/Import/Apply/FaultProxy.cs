using System.Reflection;

namespace ShipLean.WebApi.Tests.Import.Apply;

// Wraps a real service and lets a test replace one call's result: the hook gets the method, its arguments and the real service, and returns a replacement result, or null to call through.
public class FaultProxy<T> : DispatchProxy
    where T : class
{
    public T? Inner { get; set; }

    public Func<MethodInfo, object?[]?, T, object?>? Fault { get; set; }

    public static T Wrap(T inner, Func<MethodInfo, object?[]?, T, object?> fault)
    {
        var proxy = Create<T, FaultProxy<T>>();
        var typed = (FaultProxy<T>)(object)proxy;
        typed.Inner = inner;
        typed.Fault = fault;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        try
        {
            return Fault?.Invoke(targetMethod, args, Inner!) ?? targetMethod.Invoke(Inner, args);
        }
        catch (TargetInvocationException wrapped) when (wrapped.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
            throw;
        }
    }
}
