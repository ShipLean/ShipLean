using Microsoft.Extensions.DependencyInjection;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

[TestFixture]
public sealed class AuthorizeOnFakesTests : AuthorizeConsentJourneyTests
{
    protected override Action<IServiceCollection>? ConfigureTestServices => PinnedAgentFakes.Pin;
}
