namespace ShipLean.Contracts.Links;

// A pull request, merge request or issue as the provider reports it (an issue is only Open or Closed).
public enum LinkState
{
    Open,
    Draft,
    Merged,
    Closed,
}
