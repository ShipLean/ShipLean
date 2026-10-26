using ShipLean.Contracts.Instance;
using ShipLean.Contracts.Preferences;
using ShipLean.Contracts.Projects;

namespace ShipLean.UnitTest.Dtos.Projects;

[TestFixture]
public class ProjectDtoTests
{
    private static bool Check<T>(string file, params string[] path) => ProjectDtoFixture.SurvivesRoundTrip<T>(ProjectDtoFixture.Read("projects", file, path));

    [Test]
    public void ProjectSummaryDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<List<ProjectSummaryDto>>("project-summaries.json").ShouldBeTrue();
    }

    [Test]
    public void ProjectDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<ProjectDto>("project.json").ShouldBeTrue();
    }

    [Test]
    public void ProjectSettingsDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<ProjectSettingsDto>("project-settings.json").ShouldBeTrue();
    }

    [Test]
    public void ProjectSettingsPut_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<ProjectSettingsPut>("project-settings-put.json").ShouldBeTrue();
    }

    [Test]
    public void ProjectCreate_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<ProjectCreate>("project-create.json").ShouldBeTrue();
    }

    [Test]
    public void KeyCheckDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<KeyCheckDto>("key-check.json", "taken").ShouldBeTrue();
        Check<KeyCheckDto>("key-check.json", "free").ShouldBeTrue();
    }

    [Test]
    public void MemberDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<List<MemberDto>>("members.json").ShouldBeTrue();
    }

    [Test]
    public void StateDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<List<StateDto>>("states.json").ShouldBeTrue();
    }

    [Test]
    public void StateChanges_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<StateChanges>("state-changes.json").ShouldBeTrue();
    }

    [Test]
    public void InstanceDtos_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<InstanceSettingsDto>("instance.json", "settings").ShouldBeTrue();
        Check<List<InstanceProjectDto>>("instance.json", "projects").ShouldBeTrue();
    }

    [Test]
    public void UserPreferenceDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<UserPreferenceDto>("preference.json").ShouldBeTrue();
    }
}
