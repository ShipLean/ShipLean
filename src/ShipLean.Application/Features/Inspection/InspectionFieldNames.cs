namespace ShipLean.Application.Features.Inspection;

// The attribute names Inspection reads (K1 SHIPLEAN_INSPECTION_FIELD_*). Loki uses the same names with '.' replaced by '_'.
public sealed record InspectionFieldNames(string Item, string Project, string Session, string Prompt, string Repo, string Branch, string Pr)
{
    public static InspectionFieldNames Defaults { get; } = new(
        "shiplean.item", "shiplean.project", "session.id", "prompt.id", "vcs.repository.url.full", "vcs.ref.head.name", "vcs.change.id");
}
