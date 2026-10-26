namespace ShipLean.Contracts.Links;

// How a card's State is known: read from the provider (Live), reported by an agent (Reported), reported by a provider's webhook (Webhook), or not known (None).
public enum LinkStateSource
{
    Live,
    Reported,
    Webhook,
    None,
}
