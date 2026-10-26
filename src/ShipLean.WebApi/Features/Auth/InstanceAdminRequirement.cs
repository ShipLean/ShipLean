using Microsoft.AspNetCore.Authorization;

namespace ShipLean.WebApi.Features.Auth;

// The InstanceAdmin policy's requirement: the signed-in user is an active instance admin now.
public sealed class InstanceAdminRequirement : IAuthorizationRequirement
{
}
