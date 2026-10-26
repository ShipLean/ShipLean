namespace ShipLean.Application.WorkItems.History;

// How a changed value reads in history: the state's name, a person's name, the sprint's name, the parent's title, an
// instant as a date. The label is stored with the revision, so it is absolute (DisplayDate.Format), never "2h ago".
public sealed record WorkItemFieldLabels(Func<Guid, string?> State, Func<Guid, string?> User, Func<Guid, string?> Sprint, Func<Guid, string?> Item, Func<DateTimeOffset, string> Instant);
