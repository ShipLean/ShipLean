using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Users.Admin;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.Users.Admin;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Features.Auth.Proxy;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Users.Admin;

// The instance admin's Users tab. Instance admins only; the one-time password is in the response that issues it and in no read.
[InstanceAdmin]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/admin/users")]
public sealed class UsersAdminController(UserAdmin admin) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminUsersDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<AdminUsersDto> List(CancellationToken cancellationToken) => admin.ListAsync(cancellationToken);

    [HttpPost]
    [ProducesResponseType<AddUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<AddUserResponse> Add(AddUserRequest request, CancellationToken cancellationToken) => admin.AddAsync(request, cancellationToken);

    // Reset password, and Set up sign-in for a user with no password.
    [PasswordSignInOnly]
    [HttpPost("{id:guid}/one-time-password")]
    [ProducesResponseType<OneTimePasswordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<OneTimePasswordDto> IssueOneTimePassword(Guid id, CancellationToken cancellationToken) => admin.IssueOneTimePasswordAsync(id, cancellationToken);

    [HttpPost("{id:guid}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Unlock(Guid id, CancellationToken cancellationToken) => NoContentAfter(admin.UnlockAsync(id, cancellationToken));

    [HttpPut("{id:guid}/disabled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> SetDisabled(Guid id, SetDisabledRequest request, CancellationToken cancellationToken) => NoContentAfter(admin.SetDisabledAsync(id, request.Disabled, cancellationToken));

    [HttpPut("{id:guid}/instance-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> SetInstanceAdmin(Guid id, SetInstanceAdminRequest request, CancellationToken cancellationToken) => NoContentAfter(admin.SetInstanceAdminAsync(id, request.IsInstanceAdmin, cancellationToken));

    [PasswordSignInOnly]
    [HttpPut("{id:guid}/email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<NoContentResult> ChangeEmail(Guid id, ChangeEmailRequest request, CancellationToken cancellationToken) => NoContentAfter(admin.ChangeEmailAsync(id, request.Email, cancellationToken));
}
