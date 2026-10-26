using System.Reflection;

namespace ShipLean.TestSupport.AccessSweep;

public static class AccessSweepRows
{
    // The rows of every implementation in the loaded assemblies that reference this one. A private or internal nested type
    // is a helper of some other test (a probe, a double), never a feature's rows, so it is skipped.
    public static IReadOnlyList<AccessSweepRow> All()
    {
        var self = typeof(IAccessSweepRows).Assembly;
        var candidates = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a != self && a.GetReferencedAssemblies().Any(r => r.Name == self.GetName().Name))
            .SelectMany(LoadableTypes)
            .Where(t => !(t.IsNested && !t.IsNestedPublic));
        return From(candidates);
    }

    // Every type of the assembly that loads; a type whose dependency is missing is skipped, not fatal.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    // The rows of the concrete, parameterless-constructible implementations among the given types.
    public static IReadOnlyList<AccessSweepRow> From(IEnumerable<Type> candidates)
        => candidates
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAccessSweepRows).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .SelectMany(t => ((IAccessSweepRows)Activator.CreateInstance(t)!).Rows)
            .ToList();
}
