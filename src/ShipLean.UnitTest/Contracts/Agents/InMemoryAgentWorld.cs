using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Application.Fakes.Users;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.UnitTest.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Agents;

// The five agent fakes over a shared store, the in-memory users, and a clock the test moves.
internal sealed class InMemoryAgentWorld : AgentWorld
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryUsers _users;
    private readonly SettableCurrentUser _currentUser = new();
    private readonly InMemoryUserAccounts _accounts;
    private readonly InMemoryAgentConsent _consent;
    private readonly InMemoryAgentStore _store = new();

    public InMemoryAgentWorld()
    {
        _users = new InMemoryUsers(_clock);
        _accounts = new InMemoryUserAccounts(_users, _currentUser);
        var directory = new InMemoryUserDirectory(_users);
        var store = _store;
        Tokens = new InMemoryAgentTokens(store, directory, _accounts, _clock);
        Issuer = new InMemoryAgentTokenIssuer(store, directory, _accounts, _clock);
        Clients = new InMemoryOAuthClients(store);
        Codes = new InMemoryOAuthCodes(store, _clock);
        Authorizations = new InMemoryAgentAuthorizations(store, directory, _accounts, _clock);
        _consent = new InMemoryAgentConsent(store, Authorizations, Codes, _currentUser, _clock);
        Requests = new InMemoryOAuthRequests(store, _consent, directory, new FixedAgentAddresses());
    }

    public override IAgentTokens Tokens { get; }

    public override IAgentTokenIssuer Issuer { get; }

    public override IOAuthClients Clients { get; }

    public override IOAuthCodes Codes { get; }

    public override IAgentAuthorizations Authorizations { get; }

    public override IOAuthRequests Requests { get; }

    public override IAgentConsent ConsentFor(Guid userId)
    {
        _currentUser.Id = userId;
        return _consent;
    }

    public override DateTimeOffset Now => _clock.GetUtcNow();

    public override void Advance(TimeSpan by) => _clock.Advance(by);

    public override Task<Guid> ArrangeUserAsync() => Task.FromResult(InMemoryUserArranger.Arrange(_users, new UserSpec()).Id);

    public override Task DisableUserAsync(Guid userId) => _accounts.SetDisabledAsync(userId, true);

    public override Task ArchiveClientAsync(string clientId)
    {
        _store.Archive(clientId);
        return Task.CompletedTask;
    }

    public override async Task RotateSecurityStampAsync(Guid userId)
    {
        _currentUser.Id = userId;
        await _accounts.SignOutEverywhereElseAsync();
    }
}
