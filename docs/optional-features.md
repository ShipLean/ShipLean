# Optional features

Everything here is off, or limited, until an admin turns it on. The table says what turns each on and how to tell it is on. Variables are environment variables on ShipLean; "Instance settings" and "project settings" are pages in the app. Variable names are those the code reads (`git grep` for the name finds it).

| Feature | What turns it on | How to tell it is on |
|---|---|---|
| **Pull request webhooks** | `SHIPLEAN_GITHUB_WEBHOOK_SECRET` and/or `SHIPLEAN_GITLAB_WEBHOOK_SECRET`, plus the webhook on the provider ([webhooks.md](webhooks.md)). | The provider's endpoint answers 401 to an unsigned request instead of 404, and the webhook's delivery log shows green with `{"outcome":"pong"}`. |
| **Link cards** (state of GitHub and GitLab links) | `SHIPLEAN_GITHUB_TOKEN`; for GitLab `SHIPLEAN_GITLAB_URL` and `SHIPLEAN_GITLAB_TOKEN`; or the same under Instance settings → Integrations, which wins over the environment. `SHIPLEAN_GITHUB_API_URL` (https only) points at GitHub Enterprise. | A link to a pull request shows its Open, Merged or Closed badge. The Integrations tab shows what is set. |
| **Agent dispatch** (Start agent, board Automations, demos) | An agent connection (Instance settings → Agent connections) and a repository on the project ([agents.md](agents.md)). | The item's agent panel offers Start agent. Without a connection or repository it says why it is off. |
| **GitHub issue intake** | Four steps: Issues and Issue comments webhook events, a token with Issues read and write and Actions read, the relay deployed with `RELAY_PROPOSE_GH_TOKEN` and `RELAY_ALLOWED_USERS`, then *Take GitHub issues* on the project ([github-issues.md](github-issues.md)). Needs the webhook secret and agent dispatch. | The project's settings show every readiness line satisfied; a new issue gets a Triage item and a proposal. The relay variables are not checked by ShipLean. |
| **Semantic search** | `SHIPLEAN_EMBEDDINGS_MODEL_PATH` (the model folder). `SHIPLEAN_EMBEDDINGS=off` forces lexical only. Tuning: `SHIPLEAN_SEARCH_CUTOFF` and the other `SHIPLEAN_SEARCH_*` ([search.md](search.md)). | `GET /health` has `"search": {"mode": "semantic", "model": "name@revision"}`. Lexical mode has a `reason`. |
| **Analytics** | Instance settings → Analytics: provider (Umami or PostHog), host and site key ([analytics.md](analytics.md)). No environment variable. | The Analytics tab shows the provider, and Send test event shows up in the provider within seconds. |
| **OpenTelemetry** | `OTEL_EXPORTER_OTLP_ENDPOINT` ([observability.md](observability.md)). | Traces, metrics and logs reach the collector. Nothing is registered without it. |
| **Retro auto-themes** | `SHIPLEAN_RETRO_AUTO_THEMES=on`. It also needs semantic search; otherwise themes stay as people name them ([retrospectives.md](retrospectives.md)). Any value other than `on` or `off` stops startup. | New retro points are clustered into themes automatically, and `/health` shows semantic search. |
| **Google sign-in** | `SHIPLEAN_GOOGLE_CLIENT_ID` and `SHIPLEAN_GOOGLE_CLIENT_SECRET` ([auth.md](auth.md#sign-in-with-google)). | The sign-in page has a "Sign in with Google" button; `GET /api/auth/google/start` redirects instead of 404. |
| **Identity-proxy sign-in** | `SHIPLEAN_AUTH_PROXY_TOKEN_HEADER` (with `SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER`) or `SHIPLEAN_AUTH_PROXY_EMAIL_HEADER`; the proxy must be in `SHIPLEAN_TRUSTED_PROXIES` ([auth.md](auth.md#behind-an-identity-proxy)). | People who arrive through the proxy are signed in without a password. |
| **S3 attachment storage** | `SHIPLEAN_BLOB_S3_CONNECTION` (the first connection set wins: S3, then `SHIPLEAN_BLOB_DB_CONNECTION`, then the `SHIPLEAN_BLOB_ROOT` folder). See the Configuration section of the [README](../README.md). | Startup logs which store it picked and why. |

Not every row needs a restart: the Instance settings pages apply when saved, and the environment is read at startup.

## Which version am I on

ShipLean has no releases or tags. Every push to `main` runs `docker-publish.yml`, which publishes two images:

- `ghcr.io/shiplean/shiplean:latest`, the newest build;
- `ghcr.io/shiplean/shiplean:<sha>`, one build, named for the commit.

To see what your instance runs, open `<your instance>/health` (no sign-in needed). It answers:

```json
{ "status": "ok", "version": "1.0.0+<sha>", "search": { "mode": "lexical" } }
```

`<sha>` is the commit your build was made from. To check that your build has a fix or a feature, take the commit named in the issue reply (`<fix>`) and, in a clone of the repository, run:

```sh
git merge-base --is-ancestor <fix> <yours>
```

The order matters: the fix first, your build second. It prints nothing and exits 0 when your build **contains** the fix, and exits 1 when it does not (and 128 when a commit is unknown: `git fetch` first). The other order tests the wrong thing.

Without a clone: a build pulled from `:latest` after the publication time in the reply has the fix. To update, pull `:latest` again and restart.
