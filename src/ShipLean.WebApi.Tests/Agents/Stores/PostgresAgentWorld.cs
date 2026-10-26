using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Agents.Stores;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// The seven agent stores over Postgres, and a clock the test moves. The metadata documents are a local stub.
internal sealed class PostgresAgentWorld : AgentWorld
{
    private readonly PostgresAgentDatabase _database;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
    private readonly FixedAgentAddresses _addresses = new();

    public PostgresAgentWorld(PostgresAgentDatabase database)
    {
        _database = database;
        var dataSource = database.DataSource;
        Clients = new OAuthClients(dataSource, new StubClientMetadataDocuments(_clock), _clock);
        Requests = new OAuthRequests(dataSource, _clock);
        Authorizations = new AgentAuthorizations(dataSource, _clock);
        Codes = new OAuthCodes(dataSource, _clock);
        Tokens = new AgentTokens(dataSource, _clock);
        Issuer = new AgentTokenIssuer(dataSource, _clock);
    }

    public override IAgentTokens Tokens { get; }

    public override IAgentTokenIssuer Issuer { get; }

    public override IOAuthClients Clients { get; }

    public override IOAuthCodes Codes { get; }

    public override IAgentAuthorizations Authorizations { get; }

    public override IOAuthRequests Requests { get; }

    public FakeTimeProvider Clock => _clock;

    public PostgresAgentDatabase Database => _database;

    public override IAgentConsent ConsentFor(Guid userId)
        => new AgentConsent(_database.DataSource, Authorizations, Codes, _addresses, new FixedCurrentUser(userId), _clock);

    public override DateTimeOffset Now => _clock.GetUtcNow();

    public override void Advance(TimeSpan by) => _clock.Advance(by);

    public override Task<Guid> ArrangeUserAsync() => _database.InsertUserAsync();

    public override Task DisableUserAsync(Guid userId)
        => _database.ExecuteAsync("UPDATE app_user SET disabled_at = @now WHERE id = @userId", new { userId, now = Now });

    public override Task ArchiveClientAsync(string clientId)
        => _database.ExecuteAsync("UPDATE oauth_client SET archived_at = @now WHERE client_id = @clientId", new { clientId, now = Now });

    public override Task RotateSecurityStampAsync(Guid userId)
        => _database.ExecuteAsync("UPDATE app_user SET security_stamp = @stamp WHERE id = @userId", new { userId, stamp = Guid.NewGuid().ToString("N") });
}
