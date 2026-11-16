using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.AcceptanceTests.Harness;

// Proves the acceptance host takes a test ICodeHost: ConfigureTestServices (applied by AppHost's ConfigureTestServices) swaps it,
// and the running host's service provider hands the fake out. No HTTP: the wave-0 endpoints don't call the host.
[TestFixture]
public class TaskChangesHostTests : SeededAcceptanceTest
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddFakeCodeHost();

    [Test]
    public async Task TaskChangesHost_WhenFakeCodeHostRegistered_ShouldServeIt()
    {
        await using var scope = Host.Services.CreateAsyncScope();

        var read = await scope.ServiceProvider.GetRequiredService<ICodeHost>().ReadCommitAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.First[..7]);

        read.Status.ShouldBe(CommitReadStatus.Found);
        read.Meta!.Subject.ShouldBe("Add checkout and total");
    }
}
