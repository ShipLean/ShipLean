using System.Data.Common;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Boards;

// Reads and writes board.settings. Callers check access first (any member); the store trusts them.
public interface IBoardSettingsStore
{
    // Throws ApiException.NotFound (unknown board).
    Task<BoardSettingsDto> GetAsync(Guid boardId, CancellationToken cancellationToken = default);

    // Sections the caller changed win; sections another writer changed since BasedOn are kept and named in MergedSections.
    // The merged document is validated on write (all of it, not only the sections the caller changed: after a state moves to
    // Removed, the next put must also unmap it), and any failure is ApiException.Validation:
    //   - lane and style-rule criteria pass ICriteriaEvaluator.Validate (unknown field, disallowed operator, wrong value type);
    //   - level boards have exactly one default lane;
    //   - every column has exactly one mapping per type the board shows, with a stateId of that type that is not Removed;
    //   - the first and last columns have no WIP limit and no split;
    //   - extraFields are at most 10, distinct, and fields with the cardField use that apply to the card's type;
    //   - a removed column holds no cards, and the document is well formed.
    // An unmapped state is a warning, never an error. Also throws ApiException.NotFound (unknown board).
    // The real store reads the board row FOR UPDATE and runs the cards-in-removed-column check in the same transaction; board moves
    // (lanes C and E) re-check that the target column exists under FOR SHARE on the board row, so a move cannot land in a column a put is removing.
    Task<BoardSettingsPutResult> PutAsync(Guid boardId, BoardSettingsPut put, CancellationToken cancellationToken = default);

    // Pure. Level boards (stories: story + bug, features: feature, epics: epic): one column per non-Removed state of the primary type.
    // For every other shown type, each non-Removed state goes to the column of the same category (the k-th state of a category to
    // its k-th column); a state with no column left (Bug Triaged after Bug New, one Proposed Story state) gets a column of its own
    // after that category's last column, named after the state, mapping the type to that state and the primary type to its nearest
    // earlier state. A column no state of a type reached repeats the type's nearest earlier state. Never an empty stateId.
    // One Default lane, no rules, card fields as the seed. Taskboard: no columns, no lanes, cardReordering null.
    // Column ids here are slugs of the column name (col-in-review) and exist for defaults only: columns added later get opaque ids
    // (col-<random>), and an id is never reused after its column is removed.
    // States are taken in category order (Proposed to Completed), keeping their own order within a category.
    // Throws ArgumentException when a type the board shows has no live state.
    BoardSettingsJson BuildDefaults(BoardLevel level, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType);

    // Inside the caller's transaction (a project being created). Throws ApiException.Conflict (the project already has a board at that level).
    Task InsertAsync(Guid projectId, BoardLevel level, BoardSettingsJson settings, DbTransaction transaction, CancellationToken cancellationToken = default);

    // The project's boards, Stories, Features, Epics, Taskboard. Throws nothing: an unknown project has none.
    Task<IReadOnlyList<BoardSummaryDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
}
