using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Pages.Shell;

namespace ShipLean.AcceptanceTests.Journeys.Navigation;

// Picking a project in the side-menu picker, the way a person does.
public sealed class PickerPicks(IPage page)
{
    public ShellFrame Frame { get; } = new(page);

    // The picker option of a project, by its key (the option's text starts with the key badge).
    public ILocator Option(string key) => Frame.PickerOptions.Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(key)}") });

    public async Task PickAsync(string key)
    {
        await Frame.OpenPickerAsync();
        await Option(key).ClickAsync();
    }

    // Matches an address that ends with this path and query.
    public static Regex EndsWith(string pathAndQuery) => new(Regex.Escape(pathAndQuery) + "$");
}
