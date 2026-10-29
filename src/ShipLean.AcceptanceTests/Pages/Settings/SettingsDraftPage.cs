using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings;

// The project settings page of Storefront (SHIP): the tab list, the one Save, and the General, Members and Sprints tabs.
public sealed class SettingsDraftPage(IPage page)
{
    public ILocator Name => page.GetByTestId(SettingsTestIds.GeneralName);

    public ILocator Unsaved => page.GetByTestId(SettingsTestIds.Unsaved);

    public ILocator SaveButton => page.GetByTestId(SettingsTestIds.Save);

    public ILocator SaveError => page.GetByTestId(SettingsTestIds.SaveError);

    public ILocator ReadOnlyBanner => page.GetByTestId(SettingsTestIds.ReadOnlyBanner);

    public ILocator FieldError => page.GetByTestId(SettingsTestIds.FieldError);

    public ILocator WorkingDays => page.GetByTestId(SettingsTestIds.WorkingDays);

    public ILocator MemberRows => page.GetByTestId(SettingsTestIds.MemberRow);

    public ILocator MemberAddEmail => page.GetByTestId(SettingsTestIds.MemberAddEmail);

    public ILocator MemberAddError => page.GetByTestId(SettingsTestIds.MemberAddError);

    public ILocator SprintNaming => page.GetByTestId(SettingsTestIds.SprintNaming);

    public ILocator HolidayCountry => page.GetByTestId(SettingsTestIds.HolidayCountry);

    public ILocator VelocityWindow => page.GetByTestId(SettingsTestIds.VelocityWindow);

    private static readonly string[] WorkingDayIds =
    [
        SettingsTestIds.WorkingDay1,
        SettingsTestIds.WorkingDay2,
        SettingsTestIds.WorkingDay3,
        SettingsTestIds.WorkingDay4,
        SettingsTestIds.WorkingDay5,
        SettingsTestIds.WorkingDay6,
        SettingsTestIds.WorkingDay7,
    ];

    public ILocator WorkingDay(int isoDay) => page.GetByTestId(WorkingDayIds[isoDay - 1]);

    public ILocator TabMembers => page.GetByTestId(SettingsTestIds.TabMembers);

    public ILocator TabSprints => page.GetByTestId(SettingsTestIds.TabSprints);

    public ILocator TabGeneral => page.GetByTestId(SettingsTestIds.TabGeneral);

    public ILocator MemberRow(string email) => MemberRows.Filter(new() { HasText = email });

    public ILocator RoleOf(string email) => MemberRow(email).GetByTestId(SettingsTestIds.MemberRole);

    public async Task OpenAsync(string tab = "general") =>
        await page.GotoAsync(Links.ProjectSettings("SHIP", tab));

    public async Task AddMemberAsync(string email)
    {
        await MemberAddEmail.FillAsync(email);
        await page.GetByTestId(SettingsTestIds.MemberAdd).ClickAsync();
    }

    // Saves and waits for the response, so a following reload does not abort the write.
    public async Task<IResponse> SaveAsync() =>
        await page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/settings", "PUT", () => SaveButton.ClickAsync());
}
