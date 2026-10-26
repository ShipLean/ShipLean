using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Import.Stepper;

// /import with no project yet: the project is created first, and its key is checked live before Next opens.
[TestFixture]
public class ImportKeyTests : SeededAcceptanceTest
{
    [Test]
    public async Task Import_WhenKeyInvalid_ShouldBlockNext()
    {
        var stepper = new ImportStepperPage(Page);
        await stepper.OpenStartAsync();

        await stepper.FillCreateAsync("Platform Team", "PL4T");
        await Expect(stepper.CreateKeyResult).ToHaveAttributeAsync("data-available", "false");
        await Expect(stepper.CreateNext).ToBeDisabledAsync();

        await stepper.FillCreateAsync("Platform Team", "SHIP");
        await Expect(stepper.CreateKeyResult).ToContainTextAsync("already used");
        await Expect(stepper.CreateNext).ToBeDisabledAsync();

        await stepper.FillCreateAsync("Platform Team", "PLAT");
        await Expect(stepper.CreateKeyResult).ToHaveAttributeAsync("data-available", "true");
        await Expect(stepper.CreateNext).ToBeEnabledAsync();

        await Page.RunAndWaitForApiAsync("/api/projects", "POST", () => stepper.CreateNext.ClickAsync());
        await Expect(Page).ToHaveURLAsync(new Regex("/p/PLAT/import$"));
        await Expect(Page.GetByTestId(ImportTestIds.Stepper)).ToBeVisibleAsync();
    }
}
