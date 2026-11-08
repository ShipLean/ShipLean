namespace ShipLean.Contracts.IssueIntake;

// What IssueCommentClassifier makes of a comment (C1, D11, D12, D12b):
//   Ignore: by ShipLean's own login, from a Bot, or on a pull request;
//   Command: "/shiplean" as the first word, from a maintainer;
//   Accept: "LGTM" as the first word, in any case, from the issue author or a maintainer;
//   Reply: any other comment from the issue author or a maintainer;
//   Mirror: a comment from anyone else.
public enum IssueCommentKind
{
    Ignore,
    Command,
    Accept,
    Reply,
    Mirror,
}
