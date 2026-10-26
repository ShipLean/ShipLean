using ShipLean.Contracts.Sprints;
using ShipLean.UnitTest.Dtos.Projects;

namespace ShipLean.UnitTest.Dtos.Sprints;

[TestFixture]
public class SprintDtoTests
{
    private static bool Check<T>(string file, params string[] path) => ProjectDtoFixture.SurvivesRoundTrip<T>(ProjectDtoFixture.Read("sprints", file, path));

    [Test]
    public void SprintDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<List<SprintDto>>("sprints.json").ShouldBeTrue();
    }

    [Test]
    public void SprintCapacityDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintCapacityDto>("capacity.json", "capacity").ShouldBeTrue();
        Check<SprintCapacityPut>("capacity.json", "put").ShouldBeTrue();
    }

    [Test]
    public void SprintMetricsDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<SprintMetricsDto>("metrics.json").ShouldBeTrue();
    }

    [Test]
    public void VelocityDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<VelocityDto>("velocity.json").ShouldBeTrue();
    }

    [Test]
    public void ForecastDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<ForecastDto>("forecast.json").ShouldBeTrue();
    }

    [Test]
    public void RolloverDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        Check<RolloverDto>("rollover.json", "rollover").ShouldBeTrue();
        Check<RolloverRequest>("rollover.json", "request").ShouldBeTrue();
        Check<SprintCreate>("rollover.json", "create").ShouldBeTrue();
        Check<SprintUpdate>("rollover.json", "update").ShouldBeTrue();
    }
}
