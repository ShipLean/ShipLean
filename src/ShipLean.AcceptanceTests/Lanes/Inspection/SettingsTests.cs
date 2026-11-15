using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Inspection;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Inspection;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Inspection;

// The Inspection settings (E1): the General sub-tab's content warning gates turning content on, a member sees it read-only, and the
// instance health panel lists a mapped field the store lacks as not found.
[TestFixture]
public class SettingsTests : InspectionAcceptanceTest
{
    private const string MissingAttribute = "vcs.ref.head.name";

    // The fake store, with one more mapped field that the store does not have.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.RemoveAll<ITraceSource>();
        services.AddSingleton<ITraceSource>(new MissingFieldSource(Fake));
    };

    [Test]
    [Order(1)]
    [CancelAfter(120_000)]
    public async Task ShowContent_ShouldNeedTheWarningConfirmedBeforeItSaves()
    {
        await Page.GotoAsync(Links.ProjectSettings(ProjectKey, "inspection"));
        var showContent = Page.GetByTestId(InspectionTestIds.InspectionShowContent);
        var save = Page.GetByTestId(InspectionTestIds.InspectionSettingsSave);
        await Expect(showContent).Not.ToBeCheckedAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.InspectionContentWarning)).ToHaveCountAsync(0);

        await showContent.CheckAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.InspectionContentWarning)).ToContainTextAsync("secrets");
        await Expect(save).ToBeDisabledAsync();

        await Page.GetByTestId(InspectionTestIds.InspectionContentConfirm).CheckAsync();
        await Expect(save).ToBeEnabledAsync();
        var response = await Page.RunAndWaitForApiAsync(
            $"/api/inspection/projects/{ProjectKey}/settings", "PUT", () => save.ClickAsync());
        Assert.That(response.Ok, Is.True);

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.InspectionShowContent)).ToBeCheckedAsync();
    }

    [Test]
    [Order(2)]
    [CancelAfter(120_000)]
    public async Task Settings_ForAMember_ShouldBeReadOnly()
    {
        var tom = TestUser.Tom;
        await SignOutAsync();
        await SignInAsync(tom.Email!, tom.Password!);
        await Page.GotoAsync(Links.ProjectSettings(ProjectKey, "inspection"));

        await Expect(Page.GetByTestId(InspectionTestIds.InspectionReadOnly)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.InspectionEnabled)).ToBeDisabledAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.InspectionShowContent)).ToBeDisabledAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.InspectionSettingsSave)).ToHaveCountAsync(0);
    }

    [Test]
    [Order(3)]
    [CancelAfter(120_000)]
    public async Task HealthPanel_ShouldListAMissingFieldAsNotFound()
    {
        await Page.GotoAsync(Links.InstanceSettings("inspection"));
        await Expect(Page.GetByTestId(InspectionTestIds.InspectionHealthReachable)).ToHaveTextAsync("Yes");
        await Page.GetByTestId(InspectionTestIds.InspectionHealthCheckFields).ClickAsync();

        var rows = Page.GetByTestId(InspectionTestIds.InspectionHealthField);
        await Expect(rows).ToHaveCountAsync(3);
        var missing = rows.Filter(new() { HasText = MissingAttribute });
        await Expect(missing).ToHaveAttributeAsync("data-found", "false");
        await Expect(missing).ToContainTextAsync("not found");
        await Expect(rows.Filter(new() { HasText = "shiplean.item" })).ToHaveAttributeAsync("data-found", "true");
    }

    private sealed class MissingFieldSource(ITraceSource inner) : ITraceSource
    {
        public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct) => inner.FindRunsAsync(query, ct);

        public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct) => inner.GetRunAsync(run, ct);

        public Task<StoreHealth> CheckAsync(CancellationToken ct) => inner.CheckAsync(ct);

        public async Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct)
        {
            var fields = await inner.CheckFieldsAsync(ct);
            return [.. fields, new FieldCheck("branch", MissingAttribute, false)];
        }
    }
}
