namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// Keeps the backlog the page has: once frozen, every read answers with the last one it was given, so a teammate's save never reaches it.
internal sealed class FrozenBacklog
{
    private string? _last;
    private bool _frozen;

    public static async Task<FrozenBacklog> StartAsync(IPage page)
    {
        var frozen = new FrozenBacklog();
        await page.RouteAsync(
            url => url.Contains("/backlog?", StringComparison.Ordinal),
            async route =>
            {
                if (frozen._frozen && frozen._last is not null)
                {
                    await route.FulfillAsync(new() { ContentType = "application/json", Body = frozen._last });
                    return;
                }

                var response = await route.FetchAsync();
                frozen._last = await response.TextAsync();
                await route.FulfillAsync(new() { Response = response });
            });
        return frozen;
    }

    public void Freeze() => _frozen = true;

    public Task ThawAsync(IPage page) => page.UnrouteAllAsync(new() { Behavior = UnrouteBehavior.IgnoreErrors });
}
