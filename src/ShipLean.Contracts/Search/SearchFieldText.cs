namespace ShipLean.Contracts.Search;

// The whole text of one field, before chunking. A field with no text is left out.
public sealed record SearchFieldText(SearchField Field, string Text);
