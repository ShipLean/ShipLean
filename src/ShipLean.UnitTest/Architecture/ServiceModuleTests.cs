using System.Reflection;
using Agoda.IoC.NetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application;
using ShipLean.Contracts;
using ShipLean.WebApi;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.UnitTest.Architecture;

// Every interface in ShipLean.Contracts is a contract between features, so it has exactly one implementation
// registered: a second registration would silently win over the first, a missing one fails at first use.
[TestFixture]
public class ServiceModuleTests
{
    private static readonly Type[] NotContracts = [typeof(IServiceModule), typeof(IEndpointModule)];

    [Test]
    public void ContractInterfaces_WhenAllModulesRun_ShouldEachHaveExactlyOneRegistration()
    {
        Assembly[] assemblies = [ApiMarker.Assembly, ApplicationMarker.Assembly];
        var services = new ServiceCollection();
        services.AutoWireAssembly(assemblies, isMockMode: false);
        var configuration = new ConfigurationBuilder().Build();
        foreach (var module in ModuleScan.Find<IServiceModule>(assemblies))
        {
            module.AddServices(services, configuration);
        }

        Violations(services, ContractInterfaces()).ShouldBeEmpty();
    }

    [Test]
    public void ContractInterfaces_ShouldIncludeTheSharedContracts()
    {
        ContractInterfaces().ShouldContain(typeof(ICurrentUser));
        ContractInterfaces().ShouldContain(typeof(IProjectAccess));
        ContractInterfaces().ShouldContain(typeof(IRequestOrigin));
        ContractInterfaces().ShouldNotContain(typeof(IServiceModule));
    }

    // The check is only worth having if it can fail.
    [Test]
    public void Violations_WhenAContractHasTwoRegistrationsOrNone_ShouldReportBoth()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProjectAccess, FirstAccess>();
        services.AddSingleton<IProjectAccess, SecondAccess>();

        Violations(services, [typeof(IProjectAccess), typeof(ICurrentUser)])
            .ShouldBe(["ShipLean.Contracts.ICurrentUser: 0 registrations", "ShipLean.Contracts.IProjectAccess: 2 registrations"]);
    }

    [Test]
    public void ContractInterfaces_WhenMarkedMultipleImplementations_ShouldBeSkipped()
    {
        ContractInterfaces([typeof(IMany), typeof(IOpen<>), typeof(IProjectAccess)]).ShouldBe([typeof(IProjectAccess)]);
    }

    private static Type[] ContractInterfaces() => ContractInterfaces(typeof(IServiceModule).Assembly.GetExportedTypes());

    private static Type[] ContractInterfaces(IEnumerable<Type> types)
        => types
            .Where(t => t.IsInterface && !t.IsGenericTypeDefinition && !NotContracts.Contains(t)
                        && !t.IsDefined(typeof(MultipleImplementationsAttribute), inherit: false))
            .ToArray();

    private static List<string> Violations(IServiceCollection services, IEnumerable<Type> contracts)
        => contracts
            .Select(contract => (contract.FullName, Count: services.Count(d => d.ServiceType == contract)))
            .Where(c => c.Count != 1)
            .Select(c => $"{c.FullName}: {c.Count} registrations")
            .Order(StringComparer.Ordinal)
            .ToList();

    [MultipleImplementations]
    private interface IMany
    {
    }

    private interface IOpen<T>
    {
        T Get();
    }

    private sealed class FirstAccess : IProjectAccess
    {
        public Task<ProjectRole> RequireAsync(Guid projectId, ProjectRole minimum, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectRole.Member);

        public Task<(Guid ProjectId, ProjectRole Role)> RequireAsync(ProjectItem item, Guid id, ProjectRole minimum, CancellationToken cancellationToken = default)
            => Task.FromResult((id, ProjectRole.Member));

        public Task RequireInstanceAdminAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SecondAccess : IProjectAccess
    {
        public Task<ProjectRole> RequireAsync(Guid projectId, ProjectRole minimum, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectRole.Admin);

        public Task<(Guid ProjectId, ProjectRole Role)> RequireAsync(ProjectItem item, Guid id, ProjectRole minimum, CancellationToken cancellationToken = default)
            => Task.FromResult((id, ProjectRole.Admin));

        public Task RequireInstanceAdminAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
