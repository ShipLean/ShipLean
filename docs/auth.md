# Sign-in and access

How a request becomes a user, how a user gets into a project, and how the tests prove every route checks both.

## Sign-in

- `POST /api/auth/login` (email, password) sets the cookie `shiplean.auth`: HttpOnly, SameSite=Lax, Secure over https, 14-day sliding expiry. It returns `MeDto`. `POST /api/auth/logout` clears it.
- Every endpoint needs a signed-in user unless it is `[AllowAnonymous]` (a fallback policy). The anonymous ones are login, logout, `GET /api/auth/instance` (the sign-in page's instance name), `GET /api/health`, and the two webhook endpoints (`POST /api/webhooks/github|gitlab`, which prove the caller with the provider's shared secret: [`webhooks.md`](webhooks.md)).
- `ICurrentUser.Id` is the cookie's user. A call with no user throws; there is no default user.
- **Session end.** A session ends with 401 `session_ended` when the user's security stamp changes or the account is disabled. Admin actions (disable, one-time password, email change, instance-admin change) end the user's sessions at once, and so do a password change and "sign out other sessions". Otherwise the stamp is re-read at most once a minute per process (`UserSessions`), so another process may take up to a minute to notice. The SPA goes to `/login?state=ended`. A `session_ended` on `/api` deletes the cookie; on `/hubs` (negotiate) it does not, so the next `/api` call answers `session_ended` and deletes it, and the SPA's redirect keeps the ended state.
- **Page loads without a session.** A page load (not `/api`, not `/hubs`) with no live session redirects to `/login?returnUrl=<path and query>`; one whose session ended redirects to `/login?state=ended&returnUrl=<path and query>`. The parameter is `returnUrl`, lowercase, as everywhere else.
- **Forced password change.** A user on a one-time password gets 403 `password_change_required` from every action, including `GET /api/me`, except `[AllowPasswordChange]` ones (`POST /api/auth/password`) and anonymous ones. The SPA goes to `/login?state=change`.
- **Lockout and rate limits.** 10 failed passwords lock the account for 15 minutes (status `locked`). At most 20 attempts per email per 15 minutes (`SignInAttempts`, in memory) are refused with 401, the same refusal as a wrong password, so the answer doesn't tell whether the email exists. Only the per-connection limiter (30 logins a minute per client address, the `login` rate limiter) answers 429 `rate_limited`, with the same sentence as a wrong password. Lockout is decided under a row lock on the user, so parallel wrong passwords can't pass the limit of 10.
- **CSRF.** The cookie is SameSite=Lax, and `CrossSiteWriteGuard` refuses (403 `forbidden`) a cookie-carrying write to `/api` that has neither a JSON body nor the `X-ShipLean-Client` header (`ClientHeader.Name`). The SPA's fetch wrapper sends the header on every request.
- **Logout is client-side only.** `POST /api/auth/logout` clears the caller's cookie but does not invalidate it on the server (there is no session list; that is deferred). A stolen cookie lives until the security stamp rotates, so "Sign out everywhere else" (`POST /api/auth/sign-out-others`) or a password change is the remedy.
- **Data Protection keys are stored unencrypted** in the `data_protection_key` table, so anyone who can read the database can forge cookies. Protect database access accordingly.
- **Behind a proxy.** `X-Forwarded-For` and `X-Forwarded-Proto` are honoured only from a trusted peer, with `ForwardLimit` 1 (the last hop). Trusted peers are loopback plus whatever `SHIPLEAN_TRUSTED_PROXIES` lists (comma-separated addresses or CIDRs; an invalid entry stops startup naming the setting). Behind a proxy that sets its own header, `SHIPLEAN_FORWARDED_FOR_HEADER` names it (for example `CF-Connecting-IP`). `X-Forwarded-Host` is not applied to the request, so a redirect always goes to the app's own host. A URL handed back to a caller when `base_url` is blank (including an attachment upload or download URL) does read the last `X-Forwarded-Host` and `X-Forwarded-Proto` from a trusted peer, and ignores a cluster service name (`*.svc`), because the host the last hop dialed is not one a laptop can open. When that host cannot be worked out, see [Behind a gateway](#behind-a-gateway). The reverse proxy must keep the original `Host` header and pass the WebSocket `Upgrade` and `Connection` headers: the real-time hub compares a browser's `Origin` with the request's own host, so a proxy that rewrites `Host` (nginx's default `proxy_set_header Host $proxy_host`) makes every browser hub connection a 403. The login limiter and lockout partition on the resulting client address; an untrusted caller can't choose its own. In a container the proxy is never a loopback peer: `src/docker-compose.yml` pins its network to `172.31.250.0/24` and trusts it by default (a proxy container on that network, or a proxy on the host seen as the gateway). Misconfigured, every client shares one rate-limit bucket and cookies lose `Secure`, so set `SHIPLEAN_TRUSTED_PROXIES` to wherever the proxy really connects from.
- **Identity proxy.** To sign people in through a proxy that has already checked them, see [Behind an identity proxy](#behind-an-identity-proxy). Password sign-in stays the default.
- **Admin bootstrap.** On startup, when no user is an instance admin and both `SHIPLEAN_ADMIN_EMAIL` and `SHIPLEAN_ADMIN_PASSWORD` are set, that user is created (or found) and made an instance admin; an existing password is kept, otherwise the env one is set. In proxy mode the email alone is enough: see [What admins see](#what-admins-see).

## Behind a gateway

An MCP gateway that dials ShipLean on `shiplean.<namespace>.svc` does not tell ShipLean the public host. Set `instance_settings.base_url` to that origin. That is enough. Changing it changes the token audience, so every agent has to reconnect.

To leave `base_url` blank, do both of these:

1. Add the gateway's address to `SHIPLEAN_TRUSTED_PROXIES`. In a cluster the gateway is another pod, not loopback, so the default ignores what it sends.
2. Have the gateway send `X-Forwarded-Host` (the public host) and `X-Forwarded-Proto` (`https`). The forwarded host must not itself be a `*.svc` name.

If the dialed host is still `*.svc`, ShipLean logs one warning per process. The warning names the cause: no `X-Forwarded-Host`, the peer is not in `SHIPLEAN_TRUSTED_PROXIES`, or the forwarded host is `*.svc`. `/health` reports `publicOrigin` as `unknown: set base_url`, and the instance settings General tab says the same. `attach_file` without `base64` and a `read_attachment` download URL fail instead of returning that host. `open_url` still uses it until `base_url` is set. The same checklist is in [`mcp.md`](mcp.md#behind-a-gateway).

## Behind an identity proxy

ShipLean can trust an identity-aware proxy (oauth2-proxy, Cloudflare Access, Pomerium, Authelia and the like) to say who the person is. The first time it sees an email, it creates the user. There is no password and no second sign-in. With no proxy setting present, nothing here applies and password sign-in is unchanged.

### Turning it on

There is no mode variable. Proxy sign-in is on when `SHIPLEAN_AUTH_PROXY_TOKEN_HEADER` or `SHIPLEAN_AUTH_PROXY_EMAIL_HEADER` is set.

| Setting | Meaning |
|---|---|
| `SHIPLEAN_AUTH_PROXY_TOKEN_HEADER` | Recommended. The header carrying the proxy's signed JWT. Examples: `X-Forwarded-Access-Token` (oauth2-proxy), `Cf-Access-Jwt-Assertion` (Cloudflare Access), `X-Pomerium-Jwt-Assertion` (Pomerium). The value `Authorization` means a `Bearer` token. Identity comes from the token's claims. |
| `SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER` | Required with the token header. The token's `iss` must match it. Keys come from `<issuer>/.well-known/openid-configuration` (`jwks_uri`). |
| `SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL` | Optional. A key-set URL for issuers with no discovery document. It overrides discovery. |
| `SHIPLEAN_AUTH_PROXY_TOKEN_AUDIENCE` | Optional. When set, `aud` must contain it. Default: not checked. |
| `SHIPLEAN_AUTH_PROXY_EMAIL_CLAIM` | The claim holding the email. Default `email`. |
| `SHIPLEAN_AUTH_PROXY_NAME_CLAIM` | The claim holding the name. Default `name`. With no name, a new user's name is the part of the email before the `@`. |
| `SHIPLEAN_AUTH_PROXY_EMAIL_HEADER` | Plain-header mode, for proxies that send no token. The header holding the email. |
| `SHIPLEAN_AUTH_PROXY_NAME_HEADER` | Optional, plain-header mode only. The header holding the name. With none, a new user's name is the part of the email before the `@`. |
| `SHIPLEAN_AUTH_PROXY_SIGN_OUT_URL` | Optional. Where "Sign out" goes. Default: "Sign out" is hidden. |

Startup fails, and names the setting, when:
- the token header is set without the issuer;
- both the token header and the email header are set (one way to say who the user is, never two);
- the issuer or the JWKS URL is not an absolute `https` URL. The one exception is `http` to `127.0.0.1`, `[::1]` or `localhost` (any port), for an identity provider on the same machine. A URL with a user name or password in it is refused. A discovered `jwks_uri` follows the same rule;
- the sign-out URL is neither an absolute `http(s)` URL without a user name or password, nor a path starting with a single `/` (no `//`, no backslash).

Startup logs which sign-in is active (password, proxy token or proxy header) and the issuer. It never logs a token.

### Trust model

Use token mode. The token is signed by the identity provider, and ShipLean verifies the signature, issuer, expiry and (if set) audience on every request. Nobody can forge it, even if they reach ShipLean without going through the proxy. Set `SHIPLEAN_AUTH_PROXY_TOKEN_AUDIENCE` too. Without it, a token the same issuer made for another application also signs people in.

Header mode is only safe when nothing but the proxy can connect to ShipLean. ShipLean believes the header only from a trusted peer: loopback plus `SHIPLEAN_TRUSTED_PROXIES`, the same rule the forwarded headers follow. From any other peer the header is ignored. A peer that is trusted can still send a forged header, so keep the network closed. The proxy must strip any copy of the identity headers that a client sends, on every path, including the paths it skips, and then set its own. ShipLean reads the header on `/.well-known/` and `/api/webhooks/`, so a forged header there signs the sender in, and the cookie it gets works everywhere.

In compose, `SHIPLEAN_TRUSTED_PROXIES` defaults to the whole compose network, gateway included, and connections to a published port can come from the gateway. For header mode, set `SHIPLEAN_TRUSTED_PROXIES` to the proxy container's own address. Don't publish ShipLean's port; let only the proxy reach it over the compose network.

In token mode, a token whose `email_verified` claim is present and not true (JSON `true` or the string `"true"`) is refused. A token with no such claim is accepted (Cloudflare Access sends none). Header mode doesn't check it. Make sure your identity provider verifies emails or sends the claim, because ShipLean matches users by email.

Rules that hold in both modes:
- A token that fails verification counts as no identity. The request gets 401 on `/api` and `/hubs` and no user is created. The reason is logged, never the token.
- A disabled user is refused even with a valid identity. A proxy sign-in never re-enables an account.
- If the key endpoint can't be reached, sign-in fails with 401 and ShipLean keeps running. Keys are cached and re-read when a token names a key ShipLean doesn't know.
- ShipLean issues its ordinary `shiplean.auth` cookie after a good identity. Session end, the hub and agent consent work as in password mode. A request with a valid cookie and no identity keeps working, which matters on the paths the proxy skips.
- If the cookie belongs to one person and the proxy now names another, the request acts as the other person and the cookie is replaced.
- **Every `/api` write must send JSON or the `X-ShipLean-Client` header, with or without a cookie.** The proxy's own session is a credential the browser sends by itself, so `CrossSiteWriteGuard` guards all writes in proxy mode. A script that calls `/api` behind the proxy must send one of the two.

### Paths the proxy must skip

Agents and webhooks have no browser session at the proxy. Tell the proxy not to require sign-in on these paths:

- `/mcp`
- `/linear/graphql`. The Linear-compatible endpoint agent connections call back on. The connection's callback token is the proof, sent as `Authorization: <token>` or `Authorization: Bearer <token>`; it lives outside `/api`, so `CrossSiteWriteGuard` does not apply.
- `/oauth/token`
- `/oauth/register`
- `/oauth/revoke`
- `/.well-known/`
- `/api/uploads/`. The one-time URL token an agent got from `attach_file` is the proof ([`mcp.md`](mcp.md#upload-urls)); it works with `curl -T` and no header, so `CrossSiteWriteGuard` lets it through.
- `/api/downloads/`. The short-lived URL token an agent got from `read_attachment` is the proof ([`mcp.md`](mcp.md#download-urls)); it is a GET, so `CrossSiteWriteGuard` does not apply.
- `/api/webhooks/`, when webhooks are on. Each one proves the caller with its own shared secret ([`webhooks.md`](webhooks.md)). The sender must post JSON (for GitHub, set the content type to `application/json`), because proxy mode guards every write.

ShipLean never reads the proxy identity on `/mcp`, `/linear/graphql`, `/api/uploads/`, `/api/downloads/`, `/oauth/token`, `/oauth/register` or `/oauth/revoke`. They keep their own bearer-token check, so a proxy header named `Authorization` can't be mistaken for an agent's token. A gateway that calls `/mcp` for someone it has already signed in is a different setting, `SHIPLEAN_MCP_GATEWAY_EMAIL_HEADER` ([`mcp.md`](mcp.md#who-gets-in)). `/oauth/authorize` is a browser page that needs the signed-in person, so it stays behind the proxy.

A path the proxy skips can be reached by anyone. In token mode that's fine, because nobody can forge a token. In header mode it's only safe if the proxy strips the identity headers on those paths too.

### What users see

- There is no sign-in page. The first request creates the account. If a user with that email already exists (added by an admin, or from a Jira import), they are adopted, with their id, projects and history unchanged. Emails match without regard to case.
- The name follows the identity provider. When a request carries a name that differs from the stored one, ShipLean updates it. When the provider sends no name, the stored name stays. On Your profile the name is read-only. The avatar colour can still be changed.
- There are no passwords. Login, password change, one-time passwords and "Set up sign-in" are refused with 403. There is no break-glass password, not even for instance admins.
- "Sign out" goes to `SHIPLEAN_AUTH_PROXY_SIGN_OUT_URL`. Without it the button is hidden, because the proxy would just sign the person straight back in.
- "Sign out other sessions" stays, because a stolen cookie is still a cookie.
- A request with no identity and no session gets 401 on `/api` and `/hubs`. A page load gets a plain page saying that sign-in is handled by the proxy and no identity arrived. A disabled account gets an "account disabled, ask an instance admin" page.

### What admins see

- **Add user** creates the account and shows no one-time password. Use it to add people to projects before their first visit.
- Email change and one-time passwords are gone. The email belongs to the identity provider, and changing it would break the link to that person.
- The first admin comes from `SHIPLEAN_ADMIN_EMAIL` alone. While no instance admin exists, the user with that email is made one: at startup if the user exists, at their first proxy sign-in if not. `SHIPLEAN_ADMIN_PASSWORD` is ignored with a warning. Once an admin exists, the setting does nothing. A proxy deployment needs no secret to get its first admin.
- ShipLean does not follow a person whose email changes at the provider, and does not turn off a user the provider removes. The proxy stops them reaching ShipLean. An admin can disable the account.

### Examples

Flag and field names vary between versions: check your proxy's docs.

**oauth2-proxy** (token mode). Use the identity provider's own issuer. When oauth2-proxy proxies to ShipLean itself, `--pass-access-token` sends the access token as `X-Forwarded-Access-Token`. Behind nginx `auth_request`, `--set-xauthrequest` with `--pass-access-token` returns it as `X-Auth-Request-Access-Token`, and nginx must copy it onto the request to ShipLean. An access token's `aud` depends on the provider, so check it before you set the audience.

```
SHIPLEAN_AUTH_PROXY_TOKEN_HEADER=X-Forwarded-Access-Token
SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER=https://idp.example.com/realms/main
SHIPLEAN_AUTH_PROXY_TOKEN_AUDIENCE=shiplean
SHIPLEAN_AUTH_PROXY_SIGN_OUT_URL=/oauth2/sign_out
```

Skip the paths above with oauth2-proxy's `--skip-auth-route` (or `--skip-auth-regex` in older versions). The access token must be a signed JWT with an email claim. If your provider's access token is opaque, or has no email claim, pass the ID token instead: `--pass-authorization-header` sends it as `Authorization: Bearer …`. Then set `SHIPLEAN_AUTH_PROXY_TOKEN_HEADER=Authorization` and use the OAuth client id as the audience.

**Cloudflare Access** (token mode). Cloudflare has no discovery document, so give the key-set URL.

```
SHIPLEAN_AUTH_PROXY_TOKEN_HEADER=Cf-Access-Jwt-Assertion
SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER=https://<team>.cloudflareaccess.com
SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL=https://<team>.cloudflareaccess.com/cdn-cgi/access/certs
SHIPLEAN_AUTH_PROXY_TOKEN_AUDIENCE=<the application's AUD tag>
SHIPLEAN_AUTH_PROXY_SIGN_OUT_URL=https://<team>.cloudflareaccess.com/cdn-cgi/access/logout
```

The AUD tag is on the application's overview page in the Cloudflare dashboard. For each skipped path, add a separate Access application for that path (for example `shiplean.example.com/mcp`) with a Bypass policy that includes Everyone. Cloudflare's token carries no name, so a new user's name is the part of the email before the `@`.

**Authelia** (header mode). Authelia sends no token, so ShipLean has to believe a plain header.

```
SHIPLEAN_AUTH_PROXY_EMAIL_HEADER=Remote-Email
SHIPLEAN_AUTH_PROXY_NAME_HEADER=Remote-Name
SHIPLEAN_TRUSTED_PROXIES=<the address your reverse proxy connects from>
```

Warning: anyone who can reach ShipLean directly, without going through the reverse proxy, can send `Remote-Email: admin@example.com` and be that person. Don't publish ShipLean's port. Put it on a private network with the proxy only. Note that loopback trusts every process on the host. Make the reverse proxy overwrite `Remote-Email` and `Remote-Name` from Authelia's reply rather than pass a client's copy. The skipped paths are the risk: the proxy lets them through unchecked, so make sure it still strips `Remote-Email` and `Remote-Name` there. ShipLean reads them on `/.well-known/` and `/api/webhooks/`. If you can't close the network, use a proxy that sends a token.

## Access to a project

Access is checked per project through `IProjectAccess`, first thing in every command or query that takes a project id, before any service-level validation (model binding runs first, so a body that fails to bind answers 400 before access is checked).

- `RequireAsync(projectId, ProjectRole.Member | Admin)` for a project id; `RequireAsync(ProjectItem.<Kind>, id, role)` for the id of anything inside a project (work item, sprint, board, comment, attachment, link, import job, Jira connection); it returns the project id.
- A **non-member gets `not_found`**, exactly like an unknown id, so ids leak nothing. A member below the role gets `forbidden`. **Instance admins get no implicit project access**: they are not special here.
- `[InstanceAdmin]` on an action limits it to instance admins (`app_user.is_admin` is read on every request, so demotion applies at once). Code that is not an action calls `IProjectAccess.RequireInstanceAdminAsync`.
- A route that is neither project-scoped nor instance-only (the caller's own account, preferences, the project list that filters by membership) needs no rule beyond being signed in. Those few routes are listed, with a reason, in `src/ShipLean.WebApi.Tests/Access/RouteAllowList.cs`.

## How tests sign in

- **API tests** derive from `ApiFixture`, whose requests act as `SignedInAs` (Joel, the seeded instance admin and admin of SHIP, by default) with no login round trip. Override `SignedInAs` for another `TestUser` (Priya, Tom, Mei, Sam, Kai, `Anonymous`, or `TestUser.Of(id)` for an arranged user), or act as someone else for one call: `Api.ActingAs(TestUser.Mei)`. A request carrying the real cookie is authenticated by the cookie.
- **Acceptance tests** sign in for real with a real login, once per user per class (`SignedInAs`, `SignInAsync(email, password)`, `SignOutAsync`); the cookie outlives a test, because login is rate limited.
- **Proxy tests** derive from `ProxyApiFixture` (no test sign-in, the real schemes). They use `FakeProxyIdentityReader` for a plain identity header, or `TestIdentityProvider` to mint real signed tokens and serve their key set. All three live in `ShipLean.TestSupport`.
- Neither uses a test switch in production code: the test scheme and the test `ICurrentUser` are added to the host in `ShipLeanHost.Start`.

## The access sweep and route coverage

A feature that adds a project route declares it in a file of its own: a top-level class (public or internal, not a private nested type, which the sweep skips) with a parameterless constructor, implementing `IAccessSweepRows` (`ShipLean.TestSupport.AccessSweep`), in `ShipLean.WebApi.Tests`. Only rows in that assembly are swept.

```csharp
public sealed class SprintRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/sprints", ProjectRole.Member),
        new("POST", "api/projects/{id:guid}/sprints", ProjectRole.Admin, Body: "{}"),
        new("GET", "api/projects/by-key/{key}", ProjectRole.Member, Url: "api/projects/by-key/SHIP"),
        new("DELETE", "api/projects/{id:guid}/sprints/{sprintId:guid}", ProjectRole.Admin,
            Url: $"api/projects/{SeedIds.Projects.Ship}/sprints/{SeedIds.Sprints.Sprint19}"),
        new("GET", "api/instance/settings", ProjectRole.Member, InstanceScoped: true),
    ];
}
```

`RouteTemplate` is the attribute route exactly as mapped. The sweep calls `Url` when given, otherwise the template with every `{…}` replaced by SHIP's id; `Body` is sent as JSON. **Any parameter other than a leading `api/projects/{id:guid}` means the row must set `Url`**, pointing at a real SHIP entity from `SeedIds` (`Rows_WhenTemplateHasNonProjectParameter_ShouldSetUrl`): the allowed leg then proves the entity exists, so the 404 for Kai can only come from access. **A destructive row (delete, archive) points at a seed entity used by no other row.** The allowed leg has its own database, but within it rows run in any order. `Minimum` is the least role that passes. `src/ShipLean.WebApi.Tests/Access/` runs every row, one test case each:

| Test | Caller | Expects |
|---|---|---|
| `AccessSweep_WhenNotMember_ShouldBeNotFound` | Kai, in no project | 404 `not_found` |
| `AccessSweep_WhenBelowMinimum_ShouldBeForbidden` | Mei, a SHIP member (rows with `Minimum = Admin`) | 403 `forbidden` |
| `AccessSweep_WhenAllowed_ShouldPassAccess` (`AllowedSweepTests`, a fixture and database of its own) | Joel, SHIP admin and instance admin | anything but 401, 403 or 404, and below 500 (a 400 `validation` from an empty body is fine) |
| `AccessSweep_WhenInstanceRowAndNotAdmin_ShouldBeForbidden` | Mei (instance rows) | 403 `forbidden` |

Give a row the `Body` it needs to reach the access check: binding runs before the service, so a body that fails to bind answers 400 before access is tested, and the not-member case would fail. `SweepSelfTests` proves the instance and below-minimum legs on a host that loads the test assembly's `AuthTestController` (`GET api/test-auth/admin`, `[InstanceAdmin]`) with a local instance row, since no real route exists for them. A row whose route no endpoint maps fails (`AccessSweep_WhenRowsListed_ShouldEachMatchARoute`), so a renamed route can't leave a stale row.

`Routes_WhenUnderApi_ShouldBeSweptOrAllowListed` lists every route the real app maps under `/api` and fails for any that has no sweep row and is not on the allow-list. `[InstanceAdmin]` covers a route only outside `api/projects/`, and `Routes_WhenInstanceAdmin_ShouldHaveAnInstanceRow` requires an `InstanceScoped` row for every `[InstanceAdmin]` route, so it is called as a non-admin (403) and as Joel. An action that declares no HTTP verb fails coverage. The allow-list takes no entry with a route parameter except the caller-owned routes (`CallerOwnedRoutes` in `RouteAllowList.cs`: a preference key, a connected agent id, a pending authorize request id; the service answers not_found for anyone else's) or a route readable by any signed-in user (`AnyUserRoutes`: a user's photo); never allow-list a project-scoped route: add a row. `Routes_WhenNoCookie_ShouldBeUnauthorizedUnlessAnonymous` calls every route with no cookie: the anonymous allow-list answers, every other route answers 401 `unauthorized`. Test-only controllers (`/api/test-auth/*`) are never in these lists: they exist only in hosts that add the test assembly as an application part, and the sweep's host does not.
