namespace ShipLean.Contracts.Asks;

// What a comment says about an ask, shown as a chip in Discussion: the answer to a question, a decision changed after it
// was settled, or a discussion about a decision. Label is the ask's label ("Q1", "D3").
public sealed record CommentAskTagDto(AskTagKind Kind, string Label);
