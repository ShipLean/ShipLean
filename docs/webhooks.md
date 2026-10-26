# Pull request webhooks

GitHub and GitLab can tell ShipLean when a pull or merge request opens, changes, merges or closes ([#39](https://github.com/joeldickson/ShipLean/issues/39)). ShipLean then:

- **links** the pull or merge request to each Story, Task or Bug whose key is in its title or source branch,
- **shows its state** (Open, Merged, Closed) on every link to it, and
- **closes** each linked item once its pull or merge requests have merged.

Code: `src/ShipLean.WebApi/Features/Webhooks` (the endpoints, secret and signature check) and `src/ShipLean.Application/Features/Webhooks` (the payload parsers and the shared rules). Tables: `link_state`, `webhook_delivery`, and `work_item_link.added_via`/`matched_on` ([migration](../db/migrations/202610251200_webhooks_pull_requests.sql)).

## Turning it on

Each provider has its own endpoint, turned on by its own secret. While the variable is unset, the endpoint answers 404.

| Provider | Variable | Endpoint |
|---|---|---|
| GitHub | `SHIPLEAN_GITHUB_WEBHOOK_SECRET` | `POST <base_url>/api/webhooks/github` |
| GitLab | `SHIPLEAN_GITLAB_WEBHOOK_SECRET` | `POST <base_url>/api/webhooks/gitlab` |

Pick a long random value (`openssl rand -hex 32`), set it on ShipLean, and put the same value in the webhook's secret field. There is nothing to configure inside ShipLean and no list of repositories: any repository or group that sends the right secret is accepted, and the endpoint takes any GitHub or GitLab host. Change or unset a provider's variable to rotate or turn it off; the other provider is unaffected.

### GitHub

On a repository (or an organization, for all its repositories): **Settings → Webhooks → Add webhook**.

- **Payload URL:** `<base_url>/api/webhooks/github`
- **Content type:** `application/json` (form-encoded payloads are refused)
- **Secret:** the value of `SHIPLEAN_GITHUB_WEBHOOK_SECRET`
- **Which events:** *Let me select individual events* → **Pull requests** only.

GitHub sends a `ping` when the webhook is saved; ShipLean answers `{"outcome":"pong"}`, so the delivery shows green.

### GitLab

On a project (or a group, for all its projects): **Settings → Webhooks → Add new webhook**.

- **URL:** `<base_url>/api/webhooks/gitlab`
- **Secret token:** the value of `SHIPLEAN_GITLAB_WEBHOOK_SECRET`
- **Trigger:** **Merge request events** only.

## What an event does

The GitHub endpoint acts on `pull_request` actions `opened`, `edited`, `reopened` and `closed`; the GitLab one on `Merge Request Hook` actions `open`, `update`, `reopen`, `merge` and `close`. Other events and actions are answered `{"outcome":"ignored"}`. Each parser turns its payload into one event (URL, state, title, source branch, time), and the rest is shared:

1. **Auto-link.** Keys matching `\b[A-Z]{2,4}-\d+\b` are taken from the **title** (upper case only, so `abc-123` in a sentence is not a key) and the **source branch** (any case, upper-cased: `feature/ABC-123-rate-grid`, `abc-123-fix`). Each key that is an existing **Story, Task or Bug** gets a URL link to the pull or merge request, titled with its title and marked "auto" with whether the key was in the title or branch. Features, Epics and keys that match nothing (`UTF-8`, `SHA-256`) are skipped. An item that already has a link to it, or had one that someone removed, gets no new one. Taking the key out of the title later leaves the link.
2. **Card state.** When any item links to the pull or merge request, its state, title and time are stored once for its URL, and every link to it shows the badge, with the provider and how long ago in the tooltip. An event older than the stored one changes nothing, so a late delivery can't turn a merged card back to open.
3. **Close rule**, on a merge or a close. An item that isn't in a Completed or Removed state moves to its type's first Completed state when every pull or merge request it links to is merged or closed, and at least one of them merged:
   - a merge closes it;
   - a close without a merge never does on its own: only the card changes;
   - with several, it waits for all of them; one still open, or one ShipLean has had no event for, keeps it open;
   - a reopen never reopens it.

Links are matched by URL, ignoring the scheme, the host's case, a trailing slash, a query or `#fragment`, GitLab's `/-/`, and anything after the number (`/files`, `/diffs`). The comparison is the SQL function `link_url_key`.

Writes go through the work item store as webhook writes: the revision has no person (`changed_by` is null) and names the pull or merge request, "GitHub acme/api#12" or "GitLab acme/api!7", which History shows with a "webhook" mark. The board updates live like any other change. A webhook never creates work items.

The answer lists what happened: `{"outcome":"processed","linked":["SHIP-12"],"closed":["SHIP-12"]}`. GitHub and GitLab show it in the delivery log.

## Security

- **GitHub:** `X-Hub-Signature-256` must be the HMAC-SHA256 of the raw body under the secret. **GitLab:** `X-Gitlab-Token` must be the secret. Both are compared in constant time. A wrong or missing signature is 401, logged with the delivery id and address but not the body.
- Bodies over 1 MiB are refused (413), and each client address may send 300 requests a minute (429).
- Each delivery id (`X-GitHub-Delivery`, `X-Gitlab-Event-UUID`) is recorded in `webhook_delivery` with the repository and URL; a second delivery with the same id is answered `{"outcome":"duplicate"}` and does nothing. A delivery is recorded after its work, so a provider's retry of one that failed half way is done again. Every step is safe to repeat.
- Anyone holding a provider's secret can link and then close any Story, Task or Bug whose key they guess, in any project. That is accepted for this version: `webhook_delivery` and History say which repository and pull or merge request did it, and changing the variable shuts it off.

## Not in this version

- A way to keep a linked item open on merge (a per-link "closes this item" flag).
- A generic endpoint taking the internal event shape for CI systems or scripts.
- Typed cards that fetch state themselves, and `report_link_state` for agents ([#2](https://github.com/joeldickson/ShipLean/issues/2), slice 5). Both can write to the same `link_state` rows.
