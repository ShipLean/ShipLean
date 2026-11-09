# docs

Working documentation for ShipLean that is too long for `AGENTS.md`. One file per topic.

| Doc | What |
|---|---|
| `testing.md` | The testing model: fixtures, the shared test database, test tiers and what each tier is for. |
| `auth.md` | Sign-in, sessions, the project access rule, how tests sign in, and how a feature declares access sweep rows. |
| `events.md` | Project events: the bus, the wire format, when each event is published, and the real-time hub. |
| `agents.md` | Connecting an agent: the browser sign-in and consent, scopes, tokens, revoking, and what the person sees. |
| `mcp.md` | The `/mcp` endpoint agents use: how it authenticates, the 401 and 403 answers, the limits, the Origin rule, how to connect, and how a tool is added. |
| `webhooks.md` | GitHub and GitLab pull request webhooks: turning them on, which events to tick, auto-linking by key, link card state and the close rule. |
| `github-issues.md` | GitHub issue conversations: the flow from a new issue to a closed one, setup (webhook events, token permissions, relay variables, the project setting), maintainers and `/shiplean`, rounds and the rate limit, what is posted and the public-data rule, and security. |
| `optional-features.md` | Every feature an admin turns on, what turns it on and how to tell, and how to find which version an instance runs. |
| `retrospectives.md` | Retrospectives: what agents log and when, themes, action items, and how verdicts are computed. |
| `sprint-demos.md` | Sprint demos: what a person and an agent do, BAU explained, the project's logo and tokens, the upload and its limit, playback, YouTube and the live-spike steps (the YouTube check is not proven against Google yet). |
| `search.md` | Advanced search: the `search_doc` row contract (fields, weights, chunking, staleness, what is never indexed) and the embedder, index and ranker pieces. |
| `navigation.md` | Routes, the shell, pages and their slots, and how screens link to each other. |
| `analytics.md` | Usage analytics: the three capture seams, the event catalogue, the property rules, delivery to Umami or PostHog, and how a tracked action is added. |
| `observability.md` | OpenTelemetry: turning it on, the spans, metrics and log fields emitted, what is deliberately left out, and the local Aspire dashboard profile. |
| `backups.md` | Scheduled backups: what is and isn't backed up, the bucket layout, setup in Instance settings › Backups, S3 permissions, passphrase generations, the restore runbook and troubleshooting. |
| `backup-file-format.md` | The `SLBK1` encrypted file format, byte by byte, for anyone writing their own decrypter. |

Elsewhere:

- [`AGENTS.md`](../AGENTS.md): house rules, layout and commands.
- [`design/README.md`](../design/README.md): design tokens and the reference screens.
- [`db/README.md`](../db/README.md): schema, migrations and seed.
- [Issue #1](https://github.com/joeldickson/ShipLean/issues/1): the work item model. [Issue #7](https://github.com/joeldickson/ShipLean/issues/7): the build plan this was built from.

Markdown render (`POST /api/markdown/render`) does not render images: an image drops out of the HTML.
