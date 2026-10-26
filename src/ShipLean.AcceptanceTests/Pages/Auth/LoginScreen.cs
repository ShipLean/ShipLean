using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Auth;

// The sign-in page (/login) and its states.
public sealed class LoginScreen(IPage page)
{
    public ILocator Page => page.GetByTestId(AppTestIds.LoginPage);

    public ILocator InstanceName => page.GetByTestId(AuthTestIds.InstanceName);

    public ILocator SignInForm => page.GetByTestId(AuthTestIds.SignInForm);

    public ILocator Note => page.GetByTestId(AuthTestIds.Note);

    public ILocator Failure => page.GetByTestId(AuthTestIds.Failure);

    public ILocator Password => page.GetByTestId(AuthTestIds.Password);

    public ILocator ChangeForm => page.GetByTestId(AuthTestIds.ChangeForm);

    public ILocator ChangeSubmit => page.GetByTestId(AuthTestIds.ChangeSubmit);

    public ILocator NoProjects => page.GetByTestId(AuthTestIds.NoProjects);

    public ILocator NewProject => page.GetByTestId(AuthTestIds.NewProject);

    public ILocator ImportFromJira => page.GetByTestId(AuthTestIds.ImportFromJira);

    public static string Path(string? returnUrl = null) =>
        "/login" + (returnUrl is null ? string.Empty : Harness.Links.Query(("returnUrl", returnUrl)));

    public async Task OpenAsync(string? returnUrl = null)
    {
        await page.GotoAsync(Path(returnUrl));
        await SignInForm.WaitForAsync();
    }

    public async Task SignInAsync(string email, string password)
    {
        await page.GetByTestId(AuthTestIds.Email).FillAsync(email);
        await Password.FillAsync(password);
        await page.GetByTestId(AuthTestIds.SignInSubmit).ClickAsync();
    }

    public async Task ChooseNewPasswordAsync(string newPassword)
    {
        await page.GetByTestId(AuthTestIds.NewPassword).FillAsync(newPassword);
        await page.GetByTestId(AuthTestIds.ConfirmPassword).FillAsync(newPassword);
        await ChangeSubmit.ClickAsync();
    }
}
