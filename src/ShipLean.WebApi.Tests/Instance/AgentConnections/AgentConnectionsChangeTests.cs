using System.Net;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance.AgentConnections;

// One seeded scenario: editing, regenerating and archiving one connection, in order.
[TestFixture]
public class AgentConnectionsChangeTests : ApiFixture
{
    private AgentConnectionIssuedDto _issued = null!;
    private string _route = null!;

    [OneTimeSetUp]
    public async Task CreateTheConnection()
    {
        _issued = await AgentConnectionsWire.CreateAsync(Api, "Changing relay");
        _route = $"{AgentConnectionsWire.Route}/{_issued.Connection.Id}";
    }

    [Test]
    [Order(1)]
    public async Task AgentConnections_WhenUpdated_ShouldChangeNameAndUrlAndKeepTheKeys()
    {
        using var response = await Api.PutJsonAsync(_route, new { name = "Renamed relay", receiverUrl = "https://relay2.example.com/hook" });

        var (dto, text) = await AgentConnectionsWire.ReadAsync<AgentConnectionDto>(response);
        dto.Name.ShouldBe("Renamed relay");
        dto.ReceiverUrl.ShouldBe("https://relay2.example.com/hook");
        dto.SigningSecretLast4.ShouldBe(_issued.Connection.SigningSecretLast4);
        text.ShouldNotContain(_issued.SigningSecret!);
        (await AgentConnectionsWire.FindByTokenAsync(Host, _issued.CallbackToken!))!.Name.ShouldBe("Renamed relay");
    }

    [Test]
    [Order(2)]
    public async Task AgentConnections_WhenSecretRegenerated_ShouldShowTheNewOneOnceAndSignWithIt()
    {
        using var response = await Api.PostJsonAsync($"{_route}/regenerate-secret");

        var (issued, _) = await AgentConnectionsWire.ReadAsync<AgentConnectionIssuedDto>(response);
        issued.SigningSecret.ShouldNotBeNullOrEmpty();
        issued.SigningSecret.ShouldNotBe(_issued.SigningSecret);
        issued.CallbackToken.ShouldBeNull();
        issued.Connection.SigningSecretLast4.ShouldBe(issued.SigningSecret[^4..]);
        (await AgentConnectionsWire.GetAsync(Host, _issued.Connection.Id))!.SigningSecret.ShouldBe(issued.SigningSecret);
        var (_, listText) = await AgentConnectionsWire.ReadAsync<List<AgentConnectionDto>>(await Api.GetAsync(AgentConnectionsWire.Route));
        listText.ShouldNotContain(issued.SigningSecret);
    }

    [Test]
    [Order(3)]
    public async Task AgentConnections_WhenTokenRegenerated_ShouldShowTheNewOneOnceAndStopTheOldOne()
    {
        using var response = await Api.PostJsonAsync($"{_route}/regenerate-token");

        var (issued, _) = await AgentConnectionsWire.ReadAsync<AgentConnectionIssuedDto>(response);
        issued.CallbackToken.ShouldNotBeNullOrEmpty();
        issued.SigningSecret.ShouldBeNull();
        issued.Connection.CallbackTokenLast4.ShouldBe(issued.CallbackToken[^4..]);
        (await AgentConnectionsWire.FindByTokenAsync(Host, _issued.CallbackToken!)).ShouldBeNull();
        (await AgentConnectionsWire.FindByTokenAsync(Host, issued.CallbackToken))!.Id.ShouldBe(_issued.Connection.Id);
    }

    [Test]
    [Order(4)]
    public async Task AgentConnections_WhenArchived_ShouldStayListedAndStopAuthenticatingAndRefuseChanges()
    {
        var token = (await AgentConnectionsWire.ReadAsync<AgentConnectionIssuedDto>(await Api.PostJsonAsync($"{_route}/regenerate-token"))).Value.CallbackToken!;

        using var archived = await Api.PostJsonAsync($"{_route}/archive");

        (await AgentConnectionsWire.ReadAsync<AgentConnectionDto>(archived)).Value.ArchivedAt.ShouldNotBeNull();
        (await AgentConnectionsWire.FindByTokenAsync(Host, token)).ShouldBeNull();
        (await AgentConnectionsWire.GetAsync(Host, _issued.Connection.Id))!.ArchivedAt.ShouldNotBeNull();
        var (list, _) = await AgentConnectionsWire.ReadAsync<List<AgentConnectionDto>>(await Api.GetAsync(AgentConnectionsWire.Route));
        list.Single(c => c.Id == _issued.Connection.Id).ArchivedAt.ShouldNotBeNull();
        using var update = await Api.PutJsonAsync(_route, new { name = "Again", receiverUrl = "https://relay.example.com/hook" });
        using var secret = await Api.PostJsonAsync($"{_route}/regenerate-secret");
        using var regenerated = await Api.PostJsonAsync($"{_route}/regenerate-token");
        foreach (var response in new[] { update, secret, regenerated })
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        }
    }

    [Test]
    [Order(5)]
    public async Task AgentConnections_WhenArchivedName_ShouldBeFreeToUseAgain()
    {
        var again = await AgentConnectionsWire.CreateAsync(Api, "Renamed relay");

        again.Connection.Id.ShouldNotBe(_issued.Connection.Id);
    }

    [Test]
    public async Task AgentConnections_WhenUnknownId_ShouldBeNotFound()
    {
        using var response = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{Guid.NewGuid()}/archive");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
