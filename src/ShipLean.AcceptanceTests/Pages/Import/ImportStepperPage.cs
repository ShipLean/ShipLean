using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.Jira;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Import;

// The Jira import stepper, steps 1 to 3, and the create-the-project-first screen at /import.
public sealed class ImportStepperPage(IPage page)
{
    public ILocator Stepper => page.GetByTestId(ImportTestIds.Stepper);

    public ILocator Next => page.GetByTestId(ImportTestIds.Next);

    public ILocator TestResult => page.GetByTestId(ImportTestIds.TestResult);

    public ILocator ProjectRows => page.GetByTestId(ImportTestIds.ProjectRow);

    public ILocator ImportedAs => page.GetByTestId(ImportTestIds.ProjectImportedAs);

    public ILocator CreateKeyResult => page.GetByTestId(ImportTestIds.CreateKeyResult);

    public ILocator CreateNext => page.GetByTestId(ImportTestIds.CreateNext);

    public async Task OpenAsync(string projectKey) => await page.GotoAsync(Links.JiraImport(projectKey));

    public async Task OpenStartAsync() => await page.GotoAsync(Links.JiraImportStart());

    public async Task SaveConnectionAsync(string email, string token)
    {
        await page.GetByTestId(ImportTestIds.BaseUrl).FillAsync(JiraStubHandler.CloudUrl);
        await page.GetByTestId(ImportTestIds.Email).FillAsync(email);
        await page.GetByTestId(ImportTestIds.Token).FillAsync(token);
        await page.RunAndWaitForResponseAsync(
            () => page.GetByTestId(ImportTestIds.Save).ClickAsync(),
            r => r.Request.Method == "PUT" && new Uri(r.Url).AbsolutePath.EndsWith("/jira/connection", StringComparison.Ordinal));
    }

    public async Task TestConnectionAsync() => await page.GetByTestId(ImportTestIds.Test).ClickAsync();

    public async Task PickAsync(string jiraKey) =>
        await ProjectRows.Filter(new LocatorFilterOptions { HasText = jiraKey }).First.ClickAsync();

    public async Task FillCreateAsync(string name, string key)
    {
        await page.GetByTestId(ImportTestIds.CreateName).FillAsync(name);
        await page.GetByTestId(ImportTestIds.CreateKey).FillAsync(key);
    }
}
