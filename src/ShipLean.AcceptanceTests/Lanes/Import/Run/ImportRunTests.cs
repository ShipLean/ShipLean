using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.Jira;

namespace ShipLean.AcceptanceTests.Lanes.Import.Run;

// PLATFORM's ten issues imported into the existing empty project the walk opens (no project is created), attachments included.
[TestFixture]
public class ImportRunTests : SeededAcceptanceTest
{
    private readonly JiraStubHandler _stub = new();
    private readonly AttachmentGate _gate = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportRunArrangement.PinJira(services, _stub, _gate);

    [Test]
    public async Task Import_WhenRunFromUi_ShouldShowProgressAndFinish()
    {
        await ImportRunArrangement.WalkToRunAsync(Page);
        var run = new ImportRunPage(Page);

        await run.StartAsync();

        await Expect(run.Chip).ToBeVisibleAsync();
        await run.ExpectFinishedAsync("done");
        await Expect(run.Percent).ToHaveTextAsync("100%");
        await Expect(run.IssueCount).ToContainTextAsync("10 / 10");
        await Expect(run.LogLines.First).ToBeVisibleAsync();
        await Expect(run.ErrorRows).ToHaveCountAsync(0);
    }
}
