namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// Holds the Planning page on the Backlog read it has, as StaleReorderTests does, until it sends a bulk write: every later read answers with that copy, so a
// teammate's save never reaches the page and the revisions it sends are the ones it had.
internal sealed class FrozenBacklog(IPage page)
{
    private string? _snapshot;
    private bool _frozen;

    // The page's own bulk write ends the hold, so the read that follows it is the server's.
    public async Task StartAsync()
    {
        page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal))
            {
                _frozen = false;
            }
        };
        await page.RouteAsync(
            url => url.Contains("/backlog?", StringComparison.Ordinal),
            async route =>
            {
                if (_frozen)
                {
                    await route.FulfillAsync(new() { ContentType = "application/json", Body = _snapshot });
                    return;
                }

                var response = await route.FetchAsync();
                _snapshot = await response.TextAsync();
                await route.FulfillAsync(new() { Response = response });
            });
    }

    public void Freeze() => _frozen = true;
}
