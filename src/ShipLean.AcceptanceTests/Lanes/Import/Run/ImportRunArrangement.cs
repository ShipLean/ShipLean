using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Import.Stepper;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.TestSupport.Jira;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Import.Run;

// A new project PLAT, the stepper walked through the stub's PLATFORM to the run step.
internal static class ImportRunArrangement
{
    // Pins the stub under the app's Jira client, with the gate in front of it.
    public static void PinJira(IServiceCollection services, JiraStubHandler stub, AttachmentGate gate) =>
        services.AddHttpClient(JiraTransport.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => stub)
            .AddHttpMessageHandler(() => gate);

    public static async Task WalkToRunAsync(IPage page, bool attachments = true)
    {
        var stepper = new ImportStepperPage(page);
        await stepper.OpenStartAsync();
        await stepper.FillCreateAsync("Platform Team", "PLAT");
        await Assertions.Expect(stepper.CreateNext).ToBeEnabledAsync();
        await page.RunAndWaitForApiAsync("/api/projects", "POST", () => stepper.CreateNext.ClickAsync());
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/p/PLAT/import$"));

        await stepper.SaveConnectionAsync(JiraStubHandler.CloudEmail, JiraStubHandler.CloudToken);
        await stepper.TestConnectionAsync();
        await Assertions.Expect(stepper.TestResult).ToHaveAttributeAsync("data-ok", "true");
        await stepper.Next.ClickAsync();
        await stepper.PickAsync("PLATFORM");
        await stepper.Next.ClickAsync();
        await Assertions.Expect(page.GetByTestId(ImportTestIds.TargetFixed)).ToBeVisibleAsync();
        await stepper.Next.ClickAsync();
        await Assertions.Expect(page.GetByTestId(ImportTestIds.MappingStep)).ToBeVisibleAsync();
        if (!attachments)
        {
            await page.GetByTestId(ImportTestIds.MappingIncludeAttachments).UncheckAsync();
        }

        await Assertions.Expect(stepper.Next).ToBeEnabledAsync();
        await stepper.Next.ClickAsync();
        await Assertions.Expect(new ImportRunPage(page).Start).ToBeVisibleAsync();
    }
}
