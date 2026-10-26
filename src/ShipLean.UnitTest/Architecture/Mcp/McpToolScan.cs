using System.Reflection;
using ModelContextProtocol.Server;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.UnitTest.Architecture.Mcp;

// The three rules every MCP tool follows, as scans over the types of one namespace. The scope and internal rules scan
// the whole WebApi assembly and the data-access rule ShipLean.WebApi.Features.Mcp.Tools; the self-tests scan fixture types in this assembly that break each rule.
public static class McpToolScan
{
    public const string ToolsNamespace = "ShipLean.WebApi.Features.Mcp.Tools";

    // A namespace prefix that matches every type: Program.cs registers every [McpServerToolType] in the assembly.
    public const string WholeAssembly = "";

    private static readonly string[] DataNamespaces = ["Npgsql", "Dapper"];

    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // "Type.Method" for every [McpServerTool] method with no [McpScope] on the method or on its type.
    public static IReadOnlyList<string> ToolsWithoutScope(Assembly assembly, string namespacePrefix)
        => TypesIn(assembly, namespacePrefix)
            .SelectMany(type => type.GetMethods(Methods)
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .Where(method => method.GetCustomAttribute<McpScopeAttribute>() is null && type.GetCustomAttribute<McpScopeAttribute>() is null)
                .Select(method => $"{type.FullName}.{method.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

    // "Type -> Referenced" for every type that refers to Npgsql or Dapper.
    public static IReadOnlyList<string> DataAccessReferences(Assembly assembly, string namespacePrefix)
        => TypesIn(assembly, namespacePrefix)
            .SelectMany(type => TypeReferences.Of(type)
                .Where(referenced => DataNamespaces.Any(ns => (referenced.Namespace ?? string.Empty).StartsWith(ns, StringComparison.Ordinal)))
                .Select(referenced => $"{type.FullName} -> {referenced.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToList();

    // Every [McpServerToolType] type that is not internal (public, or nested in anything).
    public static IReadOnlyList<string> NonInternalToolTypes(Assembly assembly, string namespacePrefix)
        => TypesIn(assembly, namespacePrefix)
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Where(type => type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem)
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<Type> TypesIn(Assembly assembly, string namespacePrefix)
        => assembly.GetTypes().Where(type => (type.Namespace ?? string.Empty).StartsWith(namespacePrefix, StringComparison.Ordinal));
}
