using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Usage.RetroThemeSheet;

// The retro insights Themes tab's sheet report. A member of SHIP gets in; anyone else gets not_found.
public sealed class RetroThemeSheetSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/projects/{id:guid}/usage/retro-theme-sheet-opened", ProjectRole.Member,
            Body: """{"sheet":"theme"}"""),
    ];
}
