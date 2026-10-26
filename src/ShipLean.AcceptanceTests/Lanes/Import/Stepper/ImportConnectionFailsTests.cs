using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.Jira;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Import.Stepper;

// Step 1 on Storefront, which has no Jira connection: a refused token says what to check.
[TestFixture]
public class ImportConnectionFailsTests : SeededAcceptanceTest
{
    private readonly JiraStubHandler _stub = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportStubJira.Pin(services, _stub);

    [Test]
    public async Task Import_WhenConnectionFails_ShouldShowWhatToCheck()
    {
        var stepper = new ImportStepperPage(Page);
        await stepper.OpenAsync("SHIP");
        await Expect(Page.GetByTestId(ImportTestIds.ConnectionPanel)).ToBeVisibleAsync();
        await stepper.SaveConnectionAsync(JiraStubHandler.CloudEmail, "a-token-jira-refuses");

        await Expect(Page.GetByTestId(ImportTestIds.TokenSaved)).ToContainTextAsync("ends uses");
        await stepper.TestConnectionAsync();

        await Expect(stepper.TestResult).ToHaveAttributeAsync("data-ok", "false");
        await Expect(stepper.TestResult).ToContainTextAsync("What to check");
        await Expect(stepper.Next).ToBeDisabledAsync();
    }
}
