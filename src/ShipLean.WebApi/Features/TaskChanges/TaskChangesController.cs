using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.TaskChanges.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.TaskChanges;

// A Task's or Bug's commits, their diff, the line comments on it and the review. Every route is under work-items/{id}; a code comment id is checked against that item.
[Route("api")]
public sealed class TaskChangesController(
    CommitsSectionEndpoints section, ChangesPageEndpoints page, CommitEndpoints commits, CommentEndpoints comments, ReviewEndpoints reviews) : ApiControllerBase
{
    [HttpGet("work-items/{id:guid}/commits")]
    [ProducesResponseType<TaskCommitsSectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<TaskCommitsSectionDto> Section(Guid id, CancellationToken cancellationToken)
        => section.SectionAsync(id, cancellationToken);

    [HttpGet("work-items/{id:guid}/changes")]
    [ProducesResponseType<TaskChangesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<TaskChangesDto> Page(Guid id, [FromQuery] string? commit, CancellationToken cancellationToken)
        => page.PageAsync(id, commit, cancellationToken);

    [HttpPost("work-items/{id:guid}/commits")]
    [ProducesResponseType<TaskCommitsSectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<TaskCommitsSectionDto> AddCommit(Guid id, AddTaskCommitRequest request, CancellationToken cancellationToken)
        => commits.AddAsync(id, request, cancellationToken);

    [HttpPost("work-items/{id:guid}/code-comments")]
    [ProducesResponseType<CodeCommentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<CodeCommentDto> AddComment(Guid id, AddCodeCommentRequest request, CancellationToken cancellationToken)
        => comments.AddAsync(id, request, cancellationToken);

    [HttpPost("work-items/{id:guid}/code-comments/{commentId:guid}/replies")]
    [ProducesResponseType<CodeCommentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<CodeCommentDto> ReplyToComment(Guid id, Guid commentId, ReplyCodeCommentRequest request, CancellationToken cancellationToken)
        => comments.ReplyAsync(id, commentId, request, cancellationToken);

    [HttpPost("work-items/{id:guid}/code-comments/{commentId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> ResolveComment(Guid id, Guid commentId, ResolveCodeCommentRequest request, CancellationToken cancellationToken)
        => NoContentAfter(comments.ResolveAsync(id, commentId, request, cancellationToken));

    [HttpPost("work-items/{id:guid}/changes/approve")]
    [ProducesResponseType<ReviewOutcomeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<ReviewOutcomeDto> Approve(Guid id, ApproveChangesRequest request, CancellationToken cancellationToken)
        => reviews.ApproveAsync(id, request, cancellationToken);

    [HttpPost("work-items/{id:guid}/changes/request")]
    [ProducesResponseType<ReviewOutcomeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ReviewOutcomeDto> RequestChanges(Guid id, RequestTaskChangesRequest request, CancellationToken cancellationToken)
        => reviews.RequestAsync(id, request, cancellationToken);
}
