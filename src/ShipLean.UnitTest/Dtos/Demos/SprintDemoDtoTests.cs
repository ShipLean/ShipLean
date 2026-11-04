using ShipLean.Contracts.Demos;
using ShipLean.UnitTest.Dtos.Projects;

namespace ShipLean.UnitTest.Dtos.Demos;

[TestFixture]
public class SprintDemoDtoTests
{
    private static bool Check<T>(string file) => ProjectDtoFixture.SurvivesRoundTrip<T>(ProjectDtoFixture.Read("demos", file));

    [Test]
    public void SprintDemoDto_WhenSprintDemoNoneFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintDemoDto>("sprint-demo-none.json").ShouldBeTrue();
    }

    [Test]
    public void SprintDemoDto_WhenSprintDemoWorkingFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintDemoDto>("sprint-demo-working.json").ShouldBeTrue();
    }

    [Test]
    public void SprintDemoDto_WhenSprintDemoReadyFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintDemoDto>("sprint-demo-ready.json").ShouldBeTrue();
    }

    [Test]
    public void SprintDemoDto_WhenSprintDemoYoutubeUnlistedFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintDemoDto>("sprint-demo-youtube-unlisted.json").ShouldBeTrue();
    }

    [Test]
    public void SprintDemoDto_WhenSprintDemoYoutubePrivateFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintDemoDto>("sprint-demo-youtube-private.json").ShouldBeTrue();
    }

    [Test]
    public void SprintDemoDto_WhenSprintDemoYoutubeFailedFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintDemoDto>("sprint-demo-youtube-failed.json").ShouldBeTrue();
    }

    [Test]
    public void ProjectDemoSettingsDto_WhenProjectDemoSettingsFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<ProjectDemoSettingsDto>("project-demo-settings.json").ShouldBeTrue();
    }

    [Test]
    public void ProjectDemoSettingsDto_WhenProjectDemoSettingsBrandedFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<ProjectDemoSettingsDto>("project-demo-settings-branded.json").ShouldBeTrue();
    }

    [Test]
    public void DemoBrand_WhenDemoBrandFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<DemoBrand>("demo-brand.json").ShouldBeTrue();
    }

    [Test]
    public void DemoBrand_WhenDemoBrandDefaultFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<DemoBrand>("demo-brand-default.json").ShouldBeTrue();
    }

    [Test]
    public void DemoBrand_WhenDemoBrandLogoFallbackFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<DemoBrand>("demo-brand-logo-fallback.json").ShouldBeTrue();
    }

    [Test]
    public void DemoChaptersJson_WhenDemoChaptersFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<DemoChaptersJson>("demo-chapters.json").ShouldBeTrue();
    }

    [Test]
    public void YouTubeDisconnectDto_WhenYoutubeDisconnectFixtureRoundTripped_ShouldMatchTheFixture()
    {
        Check<YouTubeDisconnectDto>("youtube-disconnect.json").ShouldBeTrue();
    }
}
