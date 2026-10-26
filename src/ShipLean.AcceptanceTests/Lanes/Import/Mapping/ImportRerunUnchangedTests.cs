using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.Jira;

namespace ShipLean.AcceptanceTests.Lanes.Import.Mapping;

// The job's saved mapping covers everything Jira offers now: a re-run steps over the mapping.
[TestFixture]
public class ImportRerunUnchangedTests : SeededAcceptanceTest
{
    private readonly JiraStubHandler _stub = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportRerunArrangement.PinJira(services, _stub);

    [Test]
    public async Task Import_WhenRerunWithoutNewRows_ShouldSkipToRun()
    {
        var jobId = await ImportRerunArrangement.ArrangeFinishedJobAsync(ConnectionString);
        var stepper = new ImportStepperPage(Page);
        var mapping = new ImportMappingPage(Page);
        await ImportRerunArrangement.SaveMappingAsync(Page, stepper, ConnectionString, jobId, _ => { });

        await ImportRerunArrangement.WalkToTargetAsync(Page, stepper);
        await Expect(mapping.Next).ToContainTextAsync("Next: Run");
        await mapping.Next.ClickAsync();

        await Expect(mapping.Run).ToBeVisibleAsync();
        await Expect(mapping.Step).ToHaveCountAsync(0);
        await Expect(mapping.MappingStepSummary).ToContainTextAsync("Same as last run");
    }
}
