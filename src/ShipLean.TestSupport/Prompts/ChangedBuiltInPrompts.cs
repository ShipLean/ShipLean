using ShipLean.Contracts.Prompts;

namespace ShipLean.TestSupport.Prompts;

// A test double for IBuiltInPrompts: wraps the real one and returns the given text (and its hash) for the chosen keys.
// Tests swap it in with ConfigureTestServices to simulate a new ShipLean release that changed a built-in.
public sealed class ChangedBuiltInPrompts(IBuiltInPrompts inner, IReadOnlyDictionary<PromptKey, string> changed) : IBuiltInPrompts
{
    public IReadOnlyList<PromptStageFacts> Stages => inner.Stages;

    public IReadOnlyList<PromptFacts> All => inner.All;

    public string ServerInstructions => inner.ServerInstructions;

    public IReadOnlyList<PromptBuiltInOnlyDto> BuiltInOnly => inner.BuiltInOnly;

    public PromptFacts Facts(PromptKey key) => inner.Facts(key);

    public string Text(PromptKey key) => changed.TryGetValue(key, out var text) ? text : inner.Text(key);

    public string Hash(PromptKey key) => changed.TryGetValue(key, out var text) ? PromptHash.Of(text) : inner.Hash(key);
}
