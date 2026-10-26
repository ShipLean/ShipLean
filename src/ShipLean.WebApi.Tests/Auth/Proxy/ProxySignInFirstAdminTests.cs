using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Proxy sign-in asks the first-admin hook (K6) once per issued cookie, before the cookie is issued, so a promoted user's
// first session already sees them as an instance admin.
[TestFixture]
public class ProxySignInFirstAdminTests : ProxyApiFixture
{
    protected override Action<IServiceCollection>? ConfigureProxyTestServices => services =>
        services.Replace(ServiceDescriptor.Singleton<IProxyFirstAdmin, RecordingFirstAdmin>());

    [Test]
    public async Task SignIn_WhenFirstSignIn_ShouldAskFirstAdminBeforeTheCookie()
    {
        var email = $"proxy-{Guid.NewGuid():N}@shiplean.test";
        using var client = ProxyClient().WithIdentity(email);

        var me = await client.GetJsonAsync<Me>("/api/me");
        await client.GetJsonAsync<Me>("/api/me");

        var calls = ((RecordingFirstAdmin)Host.Services.GetRequiredService<IProxyFirstAdmin>()).Calls.Where(c => c.Email == email).ToList();
        calls.Count.ShouldBe(1);
        calls[0].UserId.ShouldBe(me.Id);
        calls[0].CookieAlreadySet.ShouldBeFalse();
    }

    private sealed record Call(Guid UserId, string Email, bool CookieAlreadySet);

    // Resolves the accessor at call time, as CookieSession does.
    private sealed class RecordingFirstAdmin(IServiceProvider services) : IProxyFirstAdmin
    {
        private readonly ConcurrentQueue<Call> _calls = new();

        public IReadOnlyCollection<Call> Calls => _calls;

        public Task<bool> PromoteIfFirstAsync(Guid userId, string email, CancellationToken cancellationToken)
        {
            var setCookie = services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Response.Headers.SetCookie;
            _calls.Enqueue(new Call(userId, email, setCookie.Any(c => c != null && c.StartsWith(AuthServices.CookieName + "=", StringComparison.Ordinal))));
            return Task.FromResult(false);
        }
    }

    // The /api/me fields these tests read (MeDto's enums are strings on the wire).
    private sealed record Me(Guid Id, string Email, string Name, bool HasProjects);
}
