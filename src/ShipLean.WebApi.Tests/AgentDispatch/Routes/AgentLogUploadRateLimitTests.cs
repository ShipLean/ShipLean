using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.WebApi.Features.AgentDispatch;
using ShipLean.WebApi.Features.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Tests.AgentDispatch.Routes;

// The agent-log upload is anonymous, so the per-IP uploads limit (like the demo upload) is its only brake.
[TestFixture]
public class AgentLogUploadRateLimitTests
{
    [Test]
    public void Upload_ShouldCarryTheUploadsRateLimit()
    {
        var upload = typeof(AgentLogUploadController).GetMethod(nameof(AgentLogUploadController.Upload))!;

        var policies = upload.GetCustomAttributes<EnableRateLimitingAttribute>(inherit: true)
            .Concat(typeof(AgentLogUploadController).GetCustomAttributes<EnableRateLimitingAttribute>(inherit: true))
            .Select(a => a.PolicyName);

        policies.ShouldContain(UploadServices.RateLimit);
    }
}
