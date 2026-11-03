namespace ShipLean.Application.Features.Retros.Features;

// One rolled-up row of the features query: an item and what its retros (and, for a Feature, its stories' and bugs' retros) hold.
internal sealed record RetroFeatureQueryRow(
    Guid Id,
    string Key,
    string Type,
    string Title,
    Guid StateId,
    string StateName,
    string StateCategory,
    bool IsLoose,
    int Retros,
    int OwnRetros,
    DateTime LastRetroAt,
    int WentWell,
    int NotInPlan,
    int WentBadly,
    int Improve,
    int ToDecide,
    int Decidable,
    int Actions);
