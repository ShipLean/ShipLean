using System.Net;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance.AgentConnections;

// One seeded scenario: creating connections and reading them back. The secret and the token are shown once, in the answer.
[TestFixture]
public class AgentConnectionsCreateTests : ApiFixture
{
    [Test]
    public async Task AgentConnections_WhenCreated_ShouldShowSecretAndTokenOnceAndNeverInAnyRead()
    {
        var issued = await AgentConnectionsWire.CreateAsync(Api, "Home relay");

        issued.SigningSecret.ShouldNotBeNullOrEmpty();
        issued.CallbackToken.ShouldNotBeNullOrEmpty();
        issued.Connection.Name.ShouldBe("Home relay");
        issued.Connection.Type.ShouldBe(AgentConnectionTypes.LinearCompatible);
        issued.Connection.SigningSecretLast4.ShouldBe(issued.SigningSecret[^4..]);
        issued.Connection.CallbackTokenLast4.ShouldBe(issued.CallbackToken[^4..]);
        var (list, listText) = await AgentConnectionsWire.ReadAsync<List<AgentConnectionDto>>(await Api.GetAsync(AgentConnectionsWire.Route));
        list.ShouldContain(c => c.Id == issued.Connection.Id);
        listText.ShouldNotContain(issued.SigningSecret);
        listText.ShouldNotContain(issued.CallbackToken);
        listText.ShouldNotContain("signingSecret\"");
        listText.ShouldNotContain("callbackToken\"");
    }

    [Test]
    public async Task AgentConnections_WhenCreated_ShouldStoreSecretEncryptedAndTokenAsHash()
    {
        var issued = await AgentConnectionsWire.CreateAsync(Api, "Stored safely");

        await using var connection = await OpenDatabaseAsync();
        (await AgentConnectionsWire.StoredSecretAsync(connection, issued.Connection.Id)).ShouldNotContain(issued.SigningSecret!);
        var dump = await Dapper.SqlMapper.QuerySingleAsync<string>(connection, "SELECT t::text FROM agent_connection t WHERE id = @id", new { id = issued.Connection.Id });
        dump.ShouldNotContain(issued.CallbackToken!);
        var read = await AgentConnectionsWire.GetAsync(Host, issued.Connection.Id);
        read!.SigningSecret.ShouldBe(issued.SigningSecret);
        read.ReceiverUrl.ShouldBe("https://relay.example.com/hook");
        (await AgentConnectionsWire.FindByTokenAsync(Host, issued.CallbackToken!))!.Id.ShouldBe(issued.Connection.Id);
        (await AgentConnectionsWire.FindByTokenAsync(Host, "not-the-token")).ShouldBeNull();
    }

    [Test]
    public async Task AgentConnections_WhenCreatedOrRegenerated_ShouldNotBeCacheable()
    {
        using var created = await Api.PostJsonAsync(AgentConnectionsWire.Route, new { name = "No store", receiverUrl = "https://relay.example.com/hook" });
        var id = (await AgentConnectionsWire.ReadAsync<AgentConnectionIssuedDto>(created)).Value.Connection.Id;
        using var secret = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{id}/regenerate-secret");
        using var token = await Api.PostJsonAsync($"{AgentConnectionsWire.Route}/{id}/regenerate-token");

        foreach (var response in new[] { created, secret, token })
        {
            response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        }
    }

    [Test]
    public void AgentConnectionIssuedDto_WhenPrinted_ShouldShowOnlyTheConnectionId()
    {
        var dto = new AgentConnectionIssuedDto(
            new AgentConnectionDto(Guid.NewGuid(), AgentConnectionTypes.LinearCompatible, "Printed", "https://relay.example.com/hook", "cret", "oken", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null),
            "plain-signing-secret", "plain-callback-token");

        var text = dto.ToString();

        text.ShouldContain(dto.Connection.Id.ToString());
        text.ShouldNotContain("plain-signing-secret");
        text.ShouldNotContain("plain-callback-token");
    }

    [Test]
    public async Task AgentConnections_WhenSameNameCreatedConcurrently_ShouldAcceptOneAndRefuseTheRestAsValidation()
    {
        var calls = Enumerable.Range(0, 6)
            .Select(_ => Api.PostJsonAsync(AgentConnectionsWire.Route, new { name = "Racing name", receiverUrl = "https://relay.example.com/hook" }))
            .ToArray();
        var responses = await Task.WhenAll(calls);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("name");
        }
    }

    [Test]
    public async Task AgentConnections_WhenSigningSecretCannotBeDecrypted_ShouldFailGetAsNotConfiguredButStillAuthenticateByToken()
    {
        var issued = await AgentConnectionsWire.CreateAsync(Api, "Lost key ring");
        await using (var connection = await OpenDatabaseAsync())
        {
            await Dapper.SqlMapper.ExecuteAsync(connection, "UPDATE agent_connection SET signing_secret_protected = 'not-protected-data' WHERE id = @id", new { id = issued.Connection.Id });
        }

        var thrown = await Should.ThrowAsync<ApiException>(() => AgentConnectionsWire.GetAsync(Host, issued.Connection.Id));

        thrown.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        (await AgentConnectionsWire.FindByTokenAsync(Host, issued.CallbackToken!))!.Id.ShouldBe(issued.Connection.Id);
    }

    [Test]
    public async Task AgentConnections_WhenReceiverIsHttpOnLocalhost_ShouldBeAccepted()
    {
        var issued = await AgentConnectionsWire.CreateAsync(Api, "Local relay", "http://localhost:5100/hook");

        issued.Connection.ReceiverUrl.ShouldBe("http://localhost:5100/hook");
    }

    [TestCase("http://relay.example.com/hook")]
    [TestCase("ftp://relay.example.com")]
    [TestCase("not a url")]
    [TestCase("https://user:pw@relay.example.com/hook")]
    [TestCase("")]
    public async Task AgentConnections_WhenReceiverUrlInvalid_ShouldBeValidationOnThatField(string url)
    {
        using var response = await Api.PostJsonAsync(AgentConnectionsWire.Route, new { name = "Bad url", receiverUrl = url });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("receiverUrl");
    }

    [Test]
    public async Task AgentConnections_WhenNameBlankOrTaken_ShouldBeValidationOnName()
    {
        await AgentConnectionsWire.CreateAsync(Api, "Taken name");

        foreach (var name in new[] { "  ", "taken NAME" })
        {
            using var response = await Api.PostJsonAsync(AgentConnectionsWire.Route, new { name, receiverUrl = "https://relay.example.com/hook" });
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("name");
        }
    }

    [Test]
    public async Task AgentConnections_WhenNotInstanceAdmin_ShouldReturnForbiddenOnEveryRoute()
    {
        var issued = await AgentConnectionsWire.CreateAsync(Api, "Admins only");
        var mei = Api.ActingAs(TestUser.Mei);
        var route = $"{AgentConnectionsWire.Route}/{issued.Connection.Id}";
        var body = new { name = "Nope", receiverUrl = "https://relay.example.com/hook" };

        using var list = await mei.GetAsync(AgentConnectionsWire.Route);
        using var create = await mei.PostJsonAsync(AgentConnectionsWire.Route, body);
        using var update = await mei.PutJsonAsync(route, body);
        using var secret = await mei.PostJsonAsync($"{route}/regenerate-secret");
        using var token = await mei.PostJsonAsync($"{route}/regenerate-token");
        using var archive = await mei.PostJsonAsync($"{route}/archive");

        foreach (var response in new[] { list, create, update, secret, token, archive })
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        }
        (await AgentConnectionsWire.FindByTokenAsync(Host, issued.CallbackToken!)).ShouldNotBeNull();
    }
}
