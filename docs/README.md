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
| `search.md` | Advanced search: the `search_doc` row contract (fields, weights, chunking, staleness, what is never indexed) and the embedder, index and ranker pieces. |
| `navigation.md` | Routes, the shell, pages and their slots, and how screens link to each other. |

Elsewhere:

- [`AGENTS.md`](../AGENTS.md): house rules, layout and commands.
- [`design/README.md`](../design/README.md): design tokens and the reference screens.
- [`db/README.md`](../db/README.md): schema, migrations and seed.
- [Issue #1](https://github.com/joeldickson/ShipLean/issues/1): the work item model. [Issue #7](https://github.com/joeldickson/ShipLean/issues/7): the build plan this was built from.

Markdown render (`POST /api/markdown/render`) does not render images: an image drops out of the HTML.
