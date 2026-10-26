using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.Jira;
using Microsoft.Extensions.DependencyInjection;

namespace ShipLean.AcceptanceTests.Lanes.Import.Mapping;

// The job's saved mapping predates the Story status "Waiting for Vendor": a re-run shows the mapping again with that row marked.
[TestFixture]
public class ImportRerunNewStatusTests : SeededAcceptanceTest
{
    private readonly JiraStubHandler _stub = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportRerunArrangement.PinJira(services, _stub);

    [Test]
    public async Task Import_WhenRerunWithNewStatuses_ShouldShowMappingAgain()
    {
        var jobId = await ImportRerunArrangement.ArrangeFinishedJobAsync(ConnectionString);
        var stepper = new ImportStepperPage(Page);
        var mapping = new ImportMappingPage(Page);
        await ImportRerunArrangement.SaveMappingAsync(Page, stepper, ConnectionString, jobId, saved =>
        {
            var statuses = saved["statuses"]!.AsArray();
            var forgotten = statuses.Single(s => s!["jiraType"]!.GetValue<string>() == "Story" && s["jiraStatus"]!.GetValue<string>() == "Waiting for Vendor");
            statuses.Remove(forgotten);
        });

        await ImportRerunArrangement.WalkToTargetAsync(Page, stepper);
        await stepper.Next.ClickAsync();

        await Expect(mapping.Step).ToBeVisibleAsync();
        await Expect(mapping.NewStatusRows).ToHaveCountAsync(1);
        await Expect(mapping.NewStatusRows).ToContainTextAsync("Waiting for Vendor");
        await Expect(mapping.NewChips).ToHaveCountAsync(1);
        await Expect(mapping.StatusRows.First).ToBeVisibleAsync();
        await Expect(mapping.Run).ToHaveCountAsync(0);
    }
}
