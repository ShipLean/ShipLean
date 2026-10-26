using ShipLean.Contracts.Import;

namespace ShipLean.UnitTest.Dtos.Import;

[TestFixture]
public class ImportDtoTests
{
    [Test]
    public void JiraConnectionDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<JiraConnectionDto>("connection.json", "connection").ShouldBeTrue();
        ImportDtoFixture.SurvivesRoundTrip<JiraConnectionDto>("connection.json", "dataCenter").ShouldBeTrue();
    }

    [Test]
    public void JiraTestResultDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<JiraTestResultDto>("connection.json", "testOk").ShouldBeTrue();
        ImportDtoFixture.SurvivesRoundTrip<JiraTestResultDto>("connection.json", "testFailed").ShouldBeTrue();
    }

    [Test]
    public void JiraProjectDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<List<JiraProjectDto>>("projects.json").ShouldBeTrue();
    }

    [Test]
    public void ImportMappingDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<ImportMappingDto>("mapping.json").ShouldBeTrue();
    }

    [Test]
    public void ImportMappingDto_WhenSuggested_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<ImportMappingDto>("mapping-suggested.json").ShouldBeTrue();
    }

    [Test]
    public void ImportStartRequest_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<ImportStartRequest>("start-request.json").ShouldBeTrue();
    }

    [Test]
    public void ImportJobDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<ImportJobDto>("jobs.json", "running").ShouldBeTrue();
        ImportDtoFixture.SurvivesRoundTrip<ImportJobDto>("jobs.json", "finished").ShouldBeTrue();
        ImportDtoFixture.SurvivesRoundTrip<ImportJobDto>("jobs.json", "queuedRerun").ShouldBeTrue();
    }

    [Test]
    public void ImportErrorDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<List<ImportErrorDto>>("errors.json", "errors").ShouldBeTrue();
    }

    [Test]
    public void ImportLogLineDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        ImportDtoFixture.SurvivesRoundTrip<List<ImportLogLineDto>>("errors.json", "log").ShouldBeTrue();
    }

    [Test]
    public void JiraConnectionPut_WhenPrinted_ShouldHideTheToken()
    {
        var put = new JiraConnectionPut("https://acme.atlassian.net", JiraKind.Cloud, "joel@example.test", "secret-token-1234");

        put.ToString().ShouldNotContain("secret-token-1234");
    }

    [Test]
    public void JiraCredentials_WhenPrinted_ShouldHideTheToken()
    {
        var credentials = new JiraCredentials("https://acme.atlassian.net", JiraKind.Cloud, "joel@example.test", "secret-token-1234");

        credentials.ToString().ShouldNotContain("secret-token-1234");
    }
}
