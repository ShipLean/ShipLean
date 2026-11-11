using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Asks;

// The comment that answered a question: Body is its Markdown, BodyHtml the rendered HTML. By is null for a comment whose
// author has no user. AgentName is the agent that wrote it for By, otherwise null.
public sealed record QuestionAnswerDto(
    Guid CommentId,
    string Body,
    string BodyHtml,
    UserRef? By,
    string? AgentName,
    DateTimeOffset At,
    string AtLabel);
