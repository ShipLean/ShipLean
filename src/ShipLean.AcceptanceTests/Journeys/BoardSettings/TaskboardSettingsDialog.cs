using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.BoardSettings;

// The Taskboard settings dialog. It has no Save button: every change is saved as soon as it is valid.
public sealed class TaskboardSettingsDialog(IPage page)
{
    public ILocator Open => page.GetByTestId(TaskboardTestIds.SettingsOpen);

    public ILocator Dialog => page.GetByTestId(ComponentTestIds.Dialog);

    public ILocator FieldsPane => page.GetByTestId(TaskboardSettingsTestIds.FieldsPane);

    public ILocator StylesPane => page.GetByTestId(TaskboardSettingsTestIds.StylesPane);

    public ILocator ShowPoints => page.GetByTestId(TaskboardSettingsTestIds.FieldsShowPoints);

    public ILocator StyleRows => page.GetByTestId(TaskboardSettingsTestIds.StyleRow);

    public ILocator UnfinishedRule => page.GetByTestId(TaskboardSettingsTestIds.UnfinishedRule);

    public ILocator NavItem(string pane) => page.GetByTestId($"{SettingsDialogTestIds.NavItem}-{pane}");

    public ILocator Names => page.GetByTestId(TaskboardSettingsTestIds.StyleName);

    public ILocator ClauseField(int rule) => Criteria(rule).GetByTestId(CriteriaTestIds.Field).First;

    public ILocator ClauseValue(int rule) => Criteria(rule).GetByTestId(CriteriaTestIds.Value).First;

    public ILocator RemoveClause(int rule) => Criteria(rule).GetByTestId(CriteriaTestIds.Remove).First;

    public ILocator AddClause(int rule) => Criteria(rule).GetByTestId(CriteriaTestIds.Add);

    private ILocator Criteria(int rule) => page.GetByTestId(TaskboardSettingsTestIds.StyleCriteria).Nth(rule);
}
