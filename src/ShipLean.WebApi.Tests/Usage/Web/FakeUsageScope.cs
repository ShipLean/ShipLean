using ShipLean.Contracts.Usage;

namespace ShipLean.WebApi.Tests.Usage.Web;

// An IUsageScope a test fills in by hand, for exercising UsageActionFilter without a request.
public sealed class FakeUsageScope : IUsageScope
{
    private readonly Dictionary<string, object> _properties = new(StringComparer.Ordinal);

    public Guid? ProjectId { get; set; }

    public int ProjectCount { get; set; }

    public string? Viewport { get; set; }

    public IReadOnlyDictionary<string, object> Properties => _properties;

    public void ProjectTouched(Guid projectId)
    {
        ProjectId = projectId;
        ProjectCount = 1;
    }

    public void AddProperty(string key, object value) => _properties[key] = value;
}
