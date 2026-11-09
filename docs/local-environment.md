# Local environment

`ShipLean.LocalHost` runs the real ShipLean, seeded, on your machine with one command and no setup beyond Docker. It
starts a Postgres container (and Redis if you ask), builds the database from `db/`, and serves the app on a fixed port. It
is for people who want to click around or debug, for coding agents that need to check their work in a running app, and for
the demo recordings. There are no shell scripts: everything runs in the one process.

The code lives in `src/ShipLean.LocalEnvironment` (a class library) and `src/ShipLean.LocalHost` (the console exe). The
tests use the same library, so a local instance and a test host start the app the same way.

## Start

You need the .NET 10 SDK and Docker. From the repository root:

```bash
dotnet run --project src/ShipLean.LocalHost
```

When the app is ready it prints exactly these four lines, in this order:

```
ShipLean is running at http://127.0.0.1:5113
Sign in as joel@shiplean.local / joel-dev-password
SHIPLEAN_LOCAL_URL=http://127.0.0.1:5113
Press Ctrl+C to stop.
```

The email and password are the seeded instance admin (`LocalSignIn.Email` and `LocalSignIn.Password`). The first run pulls
`postgres:18`, which takes a while; later runs reuse the container.

If there is no client bundle in `src/ShipLean.WebApi/wwwroot`, one warning line starting `No client bundle in
src/ShipLean.WebApi/wwwroot:` comes first. It tells you to run `pnpm run build`, or `pnpm run dev` on :3000 (see
[The client](#the-client)). The API works without a bundle.

The host stops on Ctrl+C or SIGTERM, and also when the app itself stops. Ctrl+C during startup (while an image is
pulled or the database is built) ends the process at once, before the ready lines. Exit codes: 0 after a clean stop,
which also covers the app stopping on its own (a background service crashing, for example; the log says why), 2 for bad
arguments (the usage goes to stderr), 1 when startup fails (Docker not running, say).

## Options

```bash
dotnet run --project src/ShipLean.LocalHost -- [--port <n>] [--data fresh|keep|empty] [--database <name>] [--redis]
```

| Option | Default | Meaning |
|---|---|---|
| `--port <n>` | `5113` | The port to serve on. `0` picks a free port; read it from the ready lines. A port above 0 binds `http://localhost:<port>`, so IPv4 and IPv6 loopback both answer. |
| `--data fresh\|keep\|empty` | `fresh` | `fresh` drops the database and recreates it from today's seed template. `keep` opens the database if it exists and applies pending migrations (no seed), so your changes survive restarts; if it does not exist it behaves like `fresh`. `empty` drops it and recreates it from the schema template: the tables and no rows. |
| `--database <name>` | `shiplean_local` | The database name on the Postgres server. See the rules below. |
| `--redis` | off | Start (or reuse) a Redis container and point the app at it, so realtime events go through Redis instead of in-process. Without it the app uses its in-process realtime. |

A database name must be lowercase letters, digits and `_` only, start with `shiplean_`, and be at most 49 characters in
all (`shiplean_[a-z0-9_]{1,40}`). Names that start like the test clean-up's own databases are refused:
`shiplean_f_<10 digits>_`, `shiplean_b_`, `shiplean_schema_` and `shiplean_seed_`. Hyphens are not allowed, so a worktree
name needs its hyphens turned into underscores before it becomes a database name.

Except with `--data empty`, the host puts the seed's attachment files into the app's blob store after it starts, so seeded
attachments have their files. The store is the folder `src/ShipLean.WebApi/data/blobs` (gitignored) unless `SHIPLEAN_BLOB_ROOT` is set, and
it is shared by every local run from that checkout.

## What differs from production

The host uses the `Local` profile of `AppHost`. Nothing leaves the machine:

- The first-admin, blob-store and webhook-secret settings from your own configuration are blanked, so a developer's
  environment variables cannot reach a local instance.
- The instance's base URL is set to the address the host listens on each time it starts, so every link the app hands out
  (agent prompts, one-time upload links, `open_url`) points back at this run, not at the seed's `shiplean.larkspur.dev`.
- `HolidayRefreshService` (it calls the internet), `AgentDeliveryService` and `AgentStaleService` are removed. Webhook
  deliveries stay queued in the outbox and nothing is sent.
- The search indexer runs. It uses the real embedding model when `SHIPLEAN_EMBEDDINGS_MODEL_PATH` is set
  (`scripts/fetch-embedding-model.sh <dir>` downloads it), otherwise the app's own lexical fallback. The tests use the `Test` profile, which also removes the indexer.

## F5

Open `src/ShipLean.slnx` in your IDE and start the `LocalHost` launch profile (`Properties/launchSettings.json`). It sets
`TZ` and `PGTZ` to `UTC`, which the app and the database expect. The app runs inside the debugged process, so breakpoints in the API and the
application layer hit. The default options apply: port 5113, `fresh`, database
`shiplean_local`. Put different options in the profile's `commandLineArgs`, for example `--data keep`.

## The client

The host serves whatever is in `src/ShipLean.WebApi/wwwroot`. Two ways to get a client:

```bash
cd src && pnpm install && cd ShipLean.Client && pnpm run build   # once; the workspace is src/; writes the bundle into the WebApi's wwwroot
cd src/ShipLean.Client && pnpm run dev                                    # or: Vite on :3000 with hot reload
```

`pnpm run dev` proxies `/api` to `localhost:5113`, so use it with the default port (and keep `--port 5113`). Open
`http://localhost:3000` and sign in with the seeded account.

## How agents use it

An agent checking its own work starts its own instance and never shares one:

```bash
dotnet run --project src/ShipLean.LocalHost -- --database shiplean_<name> --port 0
```

- Choose `<name>` yourself, lowercase letters, digits and `_` only (for example `shiplean_ship319`), so two worktrees
  on one host never open the same database.
- `--port 0` takes a free port, so two instances never collide on 5113.
- Wait for the line starting `SHIPLEAN_LOCAL_URL=`; the rest of the line is the base URL. That line is the one meant for
  scripts; the others are for people.
- Run it in the background and stop it with SIGTERM (or Ctrl+C) when you are done, so the app shuts down cleanly. The
  database stays on the server afterwards; run with `--data fresh` again (the default) to start from the seed, or leave it.
- Sign in as `joel@shiplean.local` with the password on the second line.
- If the client bundle is missing, the API still answers; build the bundle first if you need the pages.

## Driving a local instance from a script

A script that has to change data on its instance (seed a Feature's `## Why`, a retro, a project's logo) can skip the browser sign-in and the OAuth consent. Start the instance with the MCP gateway header turned on:

```bash
SHIPLEAN_MCP_GATEWAY_EMAIL_HEADER=X-Demo-Email dotnet run --project src/ShipLean.LocalHost -- --database shiplean_<name> --port 0
```

Then send the header to `/mcp` and the call acts as that person, with every scope and no agent, so comments and edits are theirs:

```bash
curl -s "$SHIPLEAN_LOCAL_URL/mcp" -H 'X-Demo-Email: joel@shiplean.local' \
  -H 'content-type: application/json' -H 'accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

- LocalHost leaves this variable alone (it blanks only the first-admin, blob-store and webhook-secret settings), so the value you pass reaches the app.
- Use an email of a seeded user. A new email creates a user, who is on no project until someone adds them.
- The header is believed from anyone who can reach the port. LocalHost listens on `localhost` only; never set this variable on an instance other people can reach ([mcp.md](mcp.md#who-gets-in)).
- Only `/mcp` reads the header. For a REST call, sign in as the seeded admin with `POST /api/auth/login` and keep the cookie, and send `X-ShipLean-Client` or a JSON body on writes ([auth.md](auth.md)).
- The sprint-demo recordings use this to seed their data. A demo is made against a local seeded copy like this one, never against production ([sprint-demos.md](sprint-demos.md)). LocalHost doesn't run the YouTube forwarding loop.

## Reusing an existing Postgres or Redis

LocalHost reads the same two variables as the tests (`docs/testing.md`):

- `SHIPLEAN_TEST_DB`: a connection string to a long-lived Postgres. Set, LocalHost creates its database on that server
  and starts no container. On the shared agent host `source ~/.local/shiplean-env.sh` sets it.
- `SHIPLEAN_TEST_REDIS`: `host:port` of a long-lived Redis, used by `--redis`.

Unset, each starts a container (`postgres:18`, `redis:7.4`) that is kept running between runs, so `--data keep` still finds
its database the next time. Several named databases can live on the one server side by side; the server is shared with the
tests, whose own databases are named differently.

## Troubleshooting

- **The port is in use.** Startup fails with exit code 1 and the reason on stderr. Stop the other instance, or pass
  `--port <n>` (or `--port 0`). Keep 5113 if you use `pnpm run dev`.
- **Docker isn't running.** Startup fails with exit code 1 when no `SHIPLEAN_TEST_DB` is set and the Postgres container
  can't start. Start Docker, or point `SHIPLEAN_TEST_DB` at a Postgres you already have.
- **The seed's dates look old.** Seed dates are relative to when the database was made. A `--data keep` database ages day
  by day; run with `--data fresh` (the default) to get today's seed.
