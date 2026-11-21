namespace ShipLean.Application.Features.Prompts.Updates;

public static class PromptMerge
{
    // TODO: replaced by the real three-way merge
    public static PromptMergeResult Merge(string baseText, string mine, string theirs) => new(mine, false);
}
