namespace ShipLean.Contracts.Views;

// An extra field the board's settings ask a card to show, formatted by the server. Text is null when the field is empty.
public sealed record CardFieldDto(string FieldId, string Label, string? Text);
