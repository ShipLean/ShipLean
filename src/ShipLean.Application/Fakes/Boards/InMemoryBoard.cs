using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Fakes.Boards;

// One stored board.
internal sealed record InMemoryBoard(Guid Id, Guid ProjectId, BoardLevel Level, BoardSettingsJson Settings, DateTimeOffset UpdatedAt);
