# Connecting an agent and sending work to one

How a person connects an AI agent (Claude Code, Cursor, Claude Desktop) to ShipLean, and what the server does behind it. The transport and tools are in [`mcp.md`](mcp.md). Code: `src/ShipLean.WebApi/Features/Agents/{Clients,Authorize,Tokens}`, stores in `src/ShipLean.Application/Features/Agents/Stores`.

## Connecting

```sh
claude mcp add --transport http shiplean <base_url>/mcp
```

`<base_url>` is `instance_settings.base_url`; the page "Your profile" shows the exact address. The agent calls `/mcp`, is refused with a link to the server's metadata, and opens the browser at ShipLean's consent page.

## What the person sees

1. If signed out, the sign-in page, then back to the consent page. A person who must change their password changes it first.
2. The consent page names the agent and where it runs: "via Claude Code (claude.ai)" when the agent's host was verified, "via Claude Code (unverified)" when it registered itself. It lists the scopes asked for. A first connection asks for read and write.
3. Allow or Deny. The browser returns to the agent, which now holds a token. History and comments written by the agent carry its name.

Protocol errors on `/oauth/authorize` (a bad scope, a missing challenge) go back to the agent only when the redirect URI is a loopback one. For an https or private-use scheme redirect URI the person sees an error page instead.

"Connected agents" on Your profile lists every live connection, with when it connected and was last used, and a Revoke button.

## Scopes and step-up

| Scope | Meaning |
|---|---|
| `shiplean:read` | Always granted. Reads and every preview. |
| `shiplean:write` | Capture and update work. |
| `shiplean:plan` | Sprint planning applied for real. |

`plan` is not asked for up front. The first time a tool needs a scope the token lacks, `/mcp` answers 403 `insufficient_scope` naming the granted scopes plus the required one. The agent sends the person through consent again and the authorization grows to the union; a re-consent never narrows it and never adds a second row.

## Client identity: CIMD and DCR

- **Client ID Metadata Document (preferred).** The `client_id` is an HTTPS URL; ShipLean fetches the JSON document there (public addresses only, no redirects, small and quick) and takes the name, redirect URIs and host from it. The host is shown on the consent page and marks the client verified. A document that declares a secret or `private_key_jwt` is refused.
- **Dynamic client registration (fallback).** `POST /oauth/register` with a name and redirect URIs. Rate limited per IP. The client is unverified.

Clients are public: there is no client secret, authentication is PKCE with `S256`. 
**Redirect URIs** (one rule, `RedirectUriRules`, shared by DCR, the metadata-document parser and the stores). A redirect URI is one of:

- an `https` URL with a host;
- a loopback `http` URL (`127.0.0.1`, `[::1]`, `localhost`), where the port may differ at authorize time;
- a private-use scheme (RFC 8252 section 7.1), such as Cursor's `cursor://anysphere.cursor-retrieval/oauth/callback`: any scheme matching `^[a-z][a-z0-9+.-]*$` except `http`, `https`, `javascript`, `data`, `file`, `vbscript`, `blob`, `about`, `ws`, `wss`, `ftp`, `filesystem`, `view-source`, `mailto` and `tel`. A period in the scheme is recommended but not required.

No redirect URI may carry userinfo or a fragment, and a client may register at most 10. At authorize time the URI must match a registered one exactly (the loopback port is the only exception). The registration answer echoes the URIs as registered. The consent page shows a private-use redirect as `scheme://host` with "an app on this computer", and the SPA only navigates to http, https or an allowed private-use scheme (`features/agents/authorize/returnAddress.ts` mirrors the blocklist).

Supported clients: Claude Code (loopback or metadata document), Claude Desktop and Claude web (https), Cursor (private-use scheme, registered by DCR, so shown as unverified).

## Tokens

| Endpoint | |
|---|---|
| `POST /oauth/token` | `application/x-www-form-urlencoded`. `authorization_code`: `code`, `code_verifier`, `redirect_uri`, `client_id`, `resource`. `refresh_token`: `refresh_token`, `client_id`, `resource`. |
| `POST /oauth/revoke` | RFC 7009: `token`, `client_id`. Answers 200 for an unknown token, so a caller cannot tell whether it existed. |

- `resource` is required and must be the `/mcp` URL (`<base_url>/mcp`), else `invalid_target`.
- Success is RFC 6749 section 5.1 JSON with `Cache-Control: no-store`. Errors are section 5.2 JSON (`invalid_request`, `invalid_client`, `invalid_grant`, `unsupported_grant_type`, `invalid_target`), never the app's `ApiError`. Every refusal of a code or refresh token is the same `invalid_grant`.
- A client secret or HTTP Basic credentials are `invalid_client` (401).
- Both endpoints share a per-IP rate limit (60 a minute).
- **Lifetimes.** Access token 1 hour. Refresh token 90 days from its last use; each refresh returns a new pair. Authorization code 60 seconds, single use.
- Secrets are random and stored only as SHA-256 hashes.
- **Reuse.** A code presented twice revokes the authorization it made. A spent refresh token presented again revokes its whole family, so a stolen token that is used after the real agent has refreshed ends the connection for both.

## Revoking, and what cuts agents off

Revoke from Your profile ("Connected agents"), or the agent calls `/oauth/revoke`. The next `/mcp` call with the token is 401.

Tokens are checked on every call against the person, so these also cut every agent the person has connected, with no action on the agent's side:

- a password change, "sign out everywhere", or any other change of the person's security stamp (an email change too);
- disabling the person;
- archiving the client.

A stamp change ends the authorization for good: consenting again creates a fresh one.

**Changing `base_url` disconnects every agent.** Tokens are bound to the `/mcp` URL as their audience, so after the change they no longer match and each agent has to reconnect.

## Sending an item to an agent (board Automations)

An item reaches a connected agent through a **transition** stored in the board settings of the Stories or Features board (the Automations tab; Epics and the Taskboard have none). There is no per-state "Starts an agent" flag any more. A transition names a type (a Stories board carries story and bug, the Features board feature), a `from` state or Any state, a `to` state, and one or more **Send to agent** actions. An action has a prompt (up to 20,000 characters) and an optional connection that overrides the project's. The board settings JSON holds them as `transitions`; the pinned shape is in `src/ShipLean.TestSupport/PinnedShapes/board-settings.*.json`.

When a transition fires, `AgentTrigger` opens one session through the same starter as the panel's Start agent. The prompt goes into the delivered `promptContext` inside `<guidance>` and into the envelope's `guidance` array, beside the item, its parent and feature and the repository. Code: `src/ShipLean.Application/Features/AgentDispatch/AgentTrigger.cs`.

**What is sent.** The whole item goes inline in the Linear `promptContext` (description with acceptance criteria, links, children, the full ancestor chain, comments, attachments), so agent-relay needs no ShipLean access and no change. `<guidance>` holds the transition's prompt (also the envelope's `guidance` array, as a Linear `Team` rule). Markdown attachments are inlined in upload order up to 200 KB in all, counted as sent (the XML-escaped UTF-8 size); the first that does not fit, and every one after it, an unreadable one and every non-Markdown file are listed as links. agent-relay refuses a body over 1 MiB, so if the serialized `created` body passes about 900 KB the starter sends every attachment as a link, and if that is still too big it drops the oldest comments and says how many in `<guidance>`.

Rules and gates:

- Only a person's state change counts: a `WorkItemUpdated` whose fields include `state`. An item **created** into a state fires nothing. An import, a webhook, a named MCP agent and a bulk write (a states Save moving items, a rollover) fire nothing, and neither does a revision older than five minutes.
- A transition matches on the item's type and the move's from and to. When an exact `from` and an Any transition both match, only the exact one runs. One move opens at most one session, from the first Send to agent action of the transition that won.
- Invalid transitions never fire. A transition is Invalid when a state it names was removed or archived. It is kept, flagged Invalid in the Automations tab, and becomes valid again when a state with the same name is added back: a project-states Save re-binds transitions by state name and publishes `boardSettingsChanged` (with `transitions`) for each board it repaired.
- The usual starter gates still apply: the project needs a connection (or the action's own) and a repository, and an item with a live session starts nothing. A blocked start is logged and the panel's Start agent remains the manual route.
- The person who moved the item is the session's creator.

**Sprint rule (related, not an agent feature).** A story or bug with no sprint that a person moves from a Proposed state to an In progress state goes into the sprint running today (UTC) and the write carries the notice `sprinted`. It runs in the same write as the move, so a transition into an In progress state fires on an item that already has its sprint.
