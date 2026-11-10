namespace ShipLean.Contracts.Asks;

// Which answer a decision holds: the recommended one, one of its alternatives, or the owner's own words.
public enum AnswerKind
{
    Recommended,
    Alternative,
    Own,
}
