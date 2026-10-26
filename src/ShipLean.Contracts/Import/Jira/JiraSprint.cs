namespace ShipLean.Contracts.Import.Jira;

// State is active, closed or future. A future sprint has no dates yet (Jira serves none), so StartDate and EndDate are null;
// a sprint ShipLean creates (ISprintStore.CreateAsync needs dates) needs them, so the importer gives each undated sprint dates: future sprints are created after the last dated
// sprint, back to back, each with that sprint's length (two weeks if there is none). Goal is null when none was set.
public sealed record JiraSprint(long Id, string Name, string State, DateTimeOffset? StartDate, DateTimeOffset? EndDate, string? Goal);
