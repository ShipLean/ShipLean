using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Taskboard;

// A task card's ⋯ sheet on a phone (SHIP-722): move to a state, to another story, or to another person. Works on any board
// that draws the shared Taskboard grid, in whatever layout is on screen.
public sealed class PhoneTaskActionsPart(IPage page)
{
    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    // The card of a task by its key: the first one in the DOM, so the first state column (the first page of the pager).
    public ILocator Card(string key) =>
        page.GetByTestId(TaskboardTestIds.Card)
            .Filter(new() { Has = page.GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextRegex = new Regex($"^{key}$") }) })
            .First;

    public Task OpenAsync(string key) => Card(key).GetByTestId(TaskboardTestIds.CardActions).TapAsync();

    // Taps a state's row in the open sheet; returns the moves response.
    public Task<IResponse> MoveToAsync(string stateName) =>
        RunAndWaitForMoveAsync(() => Sheet.GetByTestId(TaskboardTestIds.ActionState).Filter(new() { HasTextRegex = new Regex($"^\\s*{stateName}") }).TapAsync());

    // Opens the story picker from the open sheet, picks the story and returns the moves response.
    public async Task<IResponse> MoveToStoryAsync(string storyKey)
    {
        await Sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasTextString = "Move to another story" }).TapAsync();
        return await RunAndWaitForMoveAsync(() => Sheet.GetByTestId(TaskboardTestIds.StoryOption).Filter(new() { HasTextString = storyKey }).TapAsync());
    }

    // Opens the assignee picker from the open sheet, picks the person and returns the moves response.
    public async Task<IResponse> AssignToAsync(string name)
    {
        await Sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasTextString = "Assignee" }).TapAsync();
        return await RunAndWaitForMoveAsync(() => Sheet.GetByTestId(TaskboardTestIds.AssigneeOption).Filter(new() { HasTextRegex = new Regex($"^\\s*{name}") }).TapAsync());
    }

    private Task<IResponse> RunAndWaitForMoveAsync(Func<Task> action) =>
        page.RunAndWaitForResponseAsync(action, r =>
            r.Request.Method == "POST"
            && (new Uri(r.Url).AbsolutePath.EndsWith("/taskboard/moves", StringComparison.Ordinal)
                || new Uri(r.Url).AbsolutePath.EndsWith("/feature-board/moves", StringComparison.Ordinal)));
}
