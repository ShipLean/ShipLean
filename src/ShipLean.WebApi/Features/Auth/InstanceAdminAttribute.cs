using Microsoft.AspNetCore.Authorization;

namespace ShipLean.WebApi.Features.Auth;

// Marks a controller or action that only an instance admin may call (403 `forbidden` otherwise).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InstanceAdminAttribute : AuthorizeAttribute
{
    public InstanceAdminAttribute() : base(AuthServices.InstanceAdminPolicy)
    {
    }
}
