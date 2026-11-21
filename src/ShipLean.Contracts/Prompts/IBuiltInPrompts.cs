namespace ShipLean.Contracts.Prompts;

// The only reader of the embedded built-in prompts.
public interface IBuiltInPrompts
{
    IReadOnlyList<PromptStageFacts> Stages { get; }

    IReadOnlyList<PromptFacts> All { get; }

    string ServerInstructions { get; }

    IReadOnlyList<PromptBuiltInOnlyDto> BuiltInOnly { get; }

    PromptFacts Facts(PromptKey key);

    string Text(PromptKey key);

    string Hash(PromptKey key);
}
