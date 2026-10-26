using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Boards;

// BasedOn is the document the caller loaded; Settings is the caller's edited copy. A section the caller changed is
// any top-level property of BoardSettingsJson where Settings differs from BasedOn.
public sealed record BoardSettingsPut(BoardSettingsJson BasedOn, BoardSettingsJson Settings);
