using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.Jira;

namespace ShipLean.AcceptanceTests.Lanes.Import.Run;

// Jira will not hand over PLATFORM-406's attachment: the import finishes with one error, which Retry cannot fix and Skip resolves.
[TestFixture]
public class ImportRunErrorsTests : SeededAcceptanceTest
{
    private readonly JiraStubHandler _stub = new();
    private readonly AttachmentGate _gate = new() { Closed = true };

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportRunArrangement.PinJira(services, _stub, _gate);

    [Test]
    public async Task Import_WhenErrors_ShouldOfferRetryAndSkip()
    {
        await ImportRunArrangement.WalkToRunAsync(Page);
        var run = new ImportRunPage(Page);

        await run.StartAsync();
        await run.ExpectFinishedAsync("doneWithErrors");
        await Expect(run.PendingErrorRows).ToHaveCountAsync(1);
        await Expect(run.PendingErrorRows).ToContainTextAsync("PLATFORM-406/10601");

        await run.RetryFirstAsync();

        await Expect(run.Busy).ToHaveCountAsync(0);
        await Expect(run.PendingErrorRows).ToHaveCountAsync(1);
        await run.ExpectFinishedAsync("doneWithErrors");

        await run.SkipFirstAsync();

        await run.ExpectFinishedAsync("done");
        await Expect(run.PendingErrorRows).ToHaveCountAsync(0);
    }
}
