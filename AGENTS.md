# ShipLean

Self-hosted lean planning tool (backlogs, boards, sprints, a sprint taskboard) built around one work item model. ASP.NET Core on .NET 10 with Npgsql + Dapper over Postgres 18, and a React/Vite SPA served from the API's `wwwroot`. It exists as a small-team replacement for the heavyweight Agile board tools: the model is [issue #1](https://github.com/joeldickson/ShipLean/issues/1), the build plan it was built from [issue #7](https://github.com/joeldickson/ShipLean/issues/7).

**UI work** (any screen, component, colour, spacing or theming): read `design/README.md` first.

## Behavioural norms

- **Don't assume.** Don't hide confusion. Surface tradeoffs and ask before guessing.
- **Simplicity first.** Minimum code that solves the problem. Nothing speculative.
- **Surgical changes.** Touch only what you must. Clean up only your own mess.
- **Goal-driven execution.** Define success criteria up front. Loop until verified.

## Project layout

| Path | What |
|---|---|
| `src/ShipLean.slnx` | Solution (XML format: build/test this, there is no `.sln`) |
| `src/ShipLean.WebApi/` | Host and controllers; error mapping and `MvcServices` in `Infrastructure/` |
| `src/ShipLean.Application/` | Commands and queries per `Features/<Feature>/`, `Database/`, `Identity/`. Embeds `db/**` |
| `src/ShipLean.Contracts/` | Interfaces, DTOs, events. `Features/<A>` reaches `Features/<B>` only through here |
| `src/ShipLean.Core/` | Dependency-free helpers (`DisplayDate`) |
| `src/ShipLean.TestSupport/` | Fixtures, `SeedIds`, builders, test ids, pinned JSON shapes. Production never references it |
| `src/ShipLean.WebApi.Tests/` | API tests: real app on Kestrel over a cloned Postgres database |
| `src/ShipLean.AcceptanceTests/` | Playwright (.NET) against the built SPA. CI shards on namespaces `Harness`, `Journeys`, `Lanes.<Area>` |
| `src/ShipLean.UnitTest/` | Last resort: pure helpers and architecture guard rails (see its `CLAUDE.md`) |
| `src/ShipLean.Client/` | React SPA, own pnpm project. `pnpm run build` writes into `ShipLean.WebApi/wwwroot`. `*.spec.tsx` = Playwright CT, `*.test.ts(x)` = vitest |
| `db/schema`, `db/migrations`, `db/seed` | Applied in that order by DbUp. Migrations are additive: `<yyyyMMddHHmm>_<area>_<name>.sql` |
| `.claude/skills/` | Repo skills referenced below (testing, UTC, API view models, IoC, …) |

## Build and test

On the shared agent host run `source ~/.local/shiplean-env.sh` first and run every `dotnet test` through `shiplean-test` (one at a time, `timeout 300`).

CI runs on GitHub-hosted runners (about 6 jobs per pull request), so:

- Verify locally before pushing; never push to see if CI passes. Batch work into few pushes.
- Run only the tests your change touches (`--filter "FullyQualifiedName~<Class>"`, your feature's namespace, the spec next to your component). The full suite is only for shared contracts, the shell, schema/seed, CI or Dockerfiles. Never the full acceptance suite locally.
- Touched C#: build Release. Touched the client: `format:check`, `lint`, `typecheck`.

```bash
dotnet build src/ShipLean.slnx -c Release                                   # warnings are errors
shiplean-test src/ShipLean.WebApi.Tests --filter "FullyQualifiedName~HealthTests"
(cd src/ShipLean.Client && pnpm install && pnpm run build)                  # acceptance tests serve this bundle; rebuild after client changes
shiplean-test src/ShipLean.AcceptanceTests --filter "FullyQualifiedName~ShipLean.AcceptanceTests.Lanes.Board."
cd src/ShipLean.Client && pnpm run format:check && pnpm run lint && pnpm run typecheck && pnpm test && pnpm run test-ct
```

## Links

- [`docs/testing.md`](docs/testing.md): fixtures, one database and host per test class, doubles, registries, test ids, traces.
- [`docs/auth.md`](docs/auth.md): sign-in, sessions, the project access rule, how tests sign in, the access sweep.
- [`docs/events.md`](docs/events.md): project events and the `X-ShipLean-Client` header.
- [`db/README.md`](db/README.md): schema rules. [`design/README.md`](design/README.md): tokens and reference screens.
- [README configuration](README.md#configuration): every `SHIPLEAN_*` variable.

## Important notes

- **Frozen files** (conductor only; stop and report if you need a change): `src/ShipLean.slnx`, `Directory.Build.props`, `*.csproj`, `src/BannedSymbols.txt`, `.editorconfig`, Dockerfiles, `docker-compose.yml`, `.github/workflows/*`, `package.json`, `pnpm-lock.yaml`, `vite.config.ts`, `tsconfig.json`, `eslint.config.js`, `playwright-ct.config.ts`, `playwright/index.tsx`, `Program.cs`, `appsettings*.json`, `AGENTS.md`, `db/schema/*`, `db/seed/*`, `SeedIds.*`.
- **Banned** (use the alternative): sleeps/`WaitForTimeout` (wait for a locator or response); `DateTime.Now`/`UtcNow`/`new Date()` for display (`TimeProvider`, server labels); `[Retry]`, `[Ignore]`, `NoWarn`, `#pragma warning disable`, `eslint-disable`, `@ts-ignore` (fix it); test-only branches in production (`ConfigureTestServices`, `page.route`, fixtures); non-`data-testid` selectors; string-built SQL; EF Core; `DELETE FROM` in `src/` or `db/` (soft delete: `archived_at` or the Removed state; `NoHardDeletesTests`); compensating for a wrong contract or editing another owner's file (stop and report); `--force`, `--amend`, `git reset --hard` on pushed history, pushing to `main`. A name from another product fails `NoForeignProductNameTests`.
- **Errors:** throw `ApiException.NotFound(...)`/`Conflict`/`Validation`/… and let `ApiErrorServices` map it to `ApiError { error, code }`. Never build error bodies by hand. The one other error body is the work item `ConflictDto` (409 on a stale revision). Enums cross the wire as camelCase strings.
- **Access first:** every command or query taking a project id calls `IProjectAccess.RequireAsync(...)` before validating the body; non-members get `not_found`, and instance admins get no implicit access.
- **Endpoints and DI:** never edit `Program.cs`. Features register via `IServiceModule` and expose HTTP through one-line controllers on `ApiControllerBase` with full `api/...` attribute routes and `[ProducesResponseType]`. `IEndpointModule` is only for non-MVC endpoints (hub, health). Own types use Agoda.IoC attributes; each `ShipLean.Contracts` interface gets exactly one registration in its own `IServiceModule` (`ServiceModuleTests`). Read configuration at resolve time, not in `AddServices`, or test overrides miss it.
- **Agents (MCP):** `/mcp` takes a bearer token, never the cookie; `/api/*` never accepts a bearer. The `/oauth/*` and `/.well-known/*` controllers must stay `[AllowAnonymous]`, or the cookie fallback policy refuses an agent with no session.
- **Dapper:** maps columns onto record constructors by name and exact type: cast in SQL (`count(*)::int`); pass timestamps as UTC (Npgsql rejects other offsets).
- **Reuse, don't rewrite:** `MarkdownRenderer` (server-side Markdig + sanitizer), `DisplayDate.Relative`/`ShortDay` for date labels (tests compute expected labels with them), `IRequestOrigin.ClientId` for which tab sent a request.
- **UTC everywhere**, no `Date` objects in the SPA (`.claude/skills/utc-everywhere/`). One request per screen with view models (`.claude/skills/api-response-for-browser/`).
- One top-level type per file. Outbound HTTP through a named `HttpClient`. Env vars are `SHIPLEAN_*` read through `IConfiguration`, never `Environment.GetEnvironmentVariable`. `TODO:` marks temporary behaviour and names its replacement.
- Tests: `Method_WhenCondition_ShouldOutcome`; never weaken or delete one (`.claude/skills/preserve-existing-tests/`).
- Playwright screenshot baselines only regenerate inside `mcr.microsoft.com/playwright:v1.62.0-resolute`; local mismatches on unchanged code are expected.
