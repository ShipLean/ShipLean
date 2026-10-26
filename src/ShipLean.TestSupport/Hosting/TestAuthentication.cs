using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.TestSupport.Hosting;

// Test sign-in, added by ShipLeanHost.Start (production has no switch for it). A request that carries the real sign-in
// cookie is authenticated by the cookie, as in production (the sign-in tests, a disabled user's cookie); any other
// request is signed in as the host's TestUser, or as the user its X-Test-User header names ("anonymous" for none), so
// one API test can act as user A and user B. Challenges and forbids still go to the cookie scheme, so an anonymous
// /api call gets 401 and a page load is redirected to /login, as in production.
public static class TestAuthentication
{
    public const string Scheme = "Test";
    public const string UserHeader = "X-Test-User";
    public const string AnonymousValue = "anonymous";

    private const string Selector = "TestOrCookie";

    public static void Add(IServiceCollection services, TestUser user)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = Selector;
                options.DefaultAuthenticateScheme = Selector;
                options.DefaultChallengeScheme = AuthServices.CookieScheme;
                options.DefaultForbidScheme = AuthServices.CookieScheme;
                options.DefaultSignInScheme = AuthServices.CookieScheme;
                options.DefaultSignOutScheme = AuthServices.CookieScheme;
            })
            .AddScheme<TestUserOptions, TestUserHandler>(Scheme, options => options.User = user)
            .AddPolicyScheme(Selector, Selector, options =>
            {
                options.ForwardDefaultSelector = context => context.Request.Cookies.ContainsKey(AuthServices.CookieName) ? AuthServices.CookieScheme : Scheme;
                options.ForwardChallenge = AuthServices.CookieScheme;
                options.ForwardForbid = AuthServices.CookieScheme;
                options.ForwardSignIn = AuthServices.CookieScheme;
                options.ForwardSignOut = AuthServices.CookieScheme;
            });
        // Services called straight from Host.Services act as the fixture's user (ShipLeanHost.ActAs).
        services.AddSingleton<ActingCurrentUser>(sp => new ActingCurrentUser(sp, new CurrentUser(sp)));
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<ActingCurrentUser>());
    }

    public sealed class TestUserOptions : AuthenticationSchemeOptions
    {
        public TestUser User { get; set; } = TestUser.Joel;
    }

    private sealed class TestUserHandler(IOptionsMonitor<TestUserOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<TestUserOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers[UserHeader].ToString();
            Guid? userId = header switch
            {
                "" => Options.User.Id,
                AnonymousValue => null,
                _ => Guid.Parse(header),
            };
            if (userId is not { } id)
            {
                return AuthenticateResult.NoResult();
            }
            // Like a cookie principal, it carries the user's current security stamp (empty for a user with no row).
            var user = await Context.RequestServices.GetRequiredService<UserSessions>().GetAsync(id, Context.RequestAborted);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(AuthPrincipal.StampClaim, user?.SecurityStamp ?? "")],
                Scheme.Name));
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
    }
}
