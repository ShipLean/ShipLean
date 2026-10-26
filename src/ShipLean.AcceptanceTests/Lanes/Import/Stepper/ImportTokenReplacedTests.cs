using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.Jira;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Import.Stepper;

// Step 1 on Storefront, which has no Jira connection: a refused token says what to check, and a good one opens step 2.
// Step 1 on Storefront: a connection saved with a bad token is replaced by a good one, which opens step 2.
[TestFixture]
public class ImportTokenReplacedTests : SeededAcceptanceTest
{
    private readonly JiraStubHandler _stub = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportStubJira.Pin(services, _stub);

    [Test]
    public async Task Import_WhenTokenReplacedWithAGoodOne_ShouldEnableNext()
    {
        var stepper = new ImportStepperPage(Page);
        await stepper.OpenAsync("SHIP");
        await stepper.SaveConnectionAsync(JiraStubHandler.CloudEmail, "a-token-jira-refuses");
        await Expect(Page.GetByTestId(ImportTestIds.TokenSaved)).ToBeVisibleAsync();
        await Page.GetByTestId(ImportTestIds.Replace).ClickAsync();
        await stepper.SaveConnectionAsync(JiraStubHandler.CloudEmail, JiraStubHandler.CloudToken);
        await stepper.TestConnectionAsync();

        await Expect(stepper.TestResult).ToHaveAttributeAsync("data-ok", "true");
        await Expect(stepper.Next).ToBeEnabledAsync();
        await stepper.Next.ClickAsync();
        await Expect(stepper.ProjectRows.First).ToBeVisibleAsync();
    }
}
