namespace ShipLean.Contracts.Asks;

// What an owner does with a decision. In-process only (DecisionAnswer); never on the wire.
public enum DecisionAnswerAction
{
    Confirm,
    Change,
    Withdraw,
}
