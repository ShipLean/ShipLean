using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests;

// Closes the run's shared Chromium when the last acceptance test has finished.
[SetUpFixture]
public class BrowserLifetime
{
    [OneTimeTearDown]
    public async Task CloseBrowserAsync() => await SharedBrowser.DisposeAsync();
}
