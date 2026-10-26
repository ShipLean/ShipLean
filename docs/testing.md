# Testing

Run `source ~/.local/shiplean-env.sh` first on the shared agent host. It sets `SHIPLEAN_TEST_DB` (the long-lived local
Postgres) and defines `shiplean-test`, a `flock`ed `timeout 300 dotnet test "$@"`: only one `dotnet test` runs on the host
at a time, so always run .NET tests through it. Without `SHIPLEAN_TEST_DB` (CI), tests start a `postgres:18` Testcontainers
container. The multi-instance realtime tests also need Redis: `SHIPLEAN_TEST_REDIS` (`host:port`), else a `redis:7.4`
container (`RedisServer`; each test class takes its own channel prefix, so they can share one server).

```bash
shiplean-test src/ShipLean.UnitTest
shiplean-test src/ShipLean.WebApi.Tests --filter "FullyQualifiedName~ShipLean.WebApi.Tests.Harness"
(cd src/ShipLean.Client && pnpm install --frozen-lockfile && pnpm run build)   # acceptance tests serve this bundle
shiplean-test src/ShipLean.AcceptanceTests --filter "FullyQualifiedName~ShipLean.AcceptanceTests.Harness."
```

## One database and one host per class

A test class is one scenario. `ApiFixture` (API tests) and `SeededAcceptanceTest` / `EmptyAcceptanceTest` (browser
tests) clone a template database for the class, start their own Kestrel instance of the real app (`ShipLeanHost`) against
it, and drop the clone at the end. Classes run in parallel; tests inside a class run in `[Order]`.

- **Templates** (`ShipLeanDatabase`): `shiplean_schema_<hash>_<yyyyMMdd>` (schema and migrations) and
  `shiplean_seed_<hash>_<yyyyMMdd>` (the schema template plus the seed). `<hash>` is `DatabaseScripts.Hash`, over the
  embedded `db/**` names and bytes; the date is the UTC run date (`TemplateKey`). Editing `db/` or a new day builds a new
  template, once, under a Postgres advisory lock (several processes and agents share the server). A clone is
  `CREATE DATABASE ... TEMPLATE ...`, well under a second, with the production locale (`FreshDatabase.Locale`).
- Templates dated before today are dropped; today's are never dropped, because worktrees with different `db/` content
  share the server. Clones and abandoned builds older than 3 hours are dropped too (by the next run that starts), so don't hold a debugging
  session on a clone past that.
- **Seeded or empty.** `ApiFixture` and `SeededAcceptanceTest` clone the seed template; `EmptyApiFixture` and
  `EmptyAcceptanceTest` clone the schema template (no rows). Override `CloneDatabaseAsync` only to add a variant.
- `FreshDatabase.CreateAsync()` still builds a database from the scripts without a template; use it only when a test needs
  to control the build itself.
- **Doubles** go through `ConfigureTestServices` (override it on the fixture). Production code has no test switches.
  Time is never frozen: the app uses `TimeProvider.System`, and a test compares times against the real clock (see `ExpectedLabels`).
- **A stopped host is never collected.** ASP.NET's rate limiting middleware starts a timer for its endpoint limiter and
  never disposes it. The timer is rooted for the life of the process, and through the limiter and the startup context it
  keeps the whole app (DI container, routing, MVC caches) alive after the host is disposed: about 3.5 MB per test class,
  so a full API run's managed heap grows to roughly 1.3 GB. Releasing references in the fixture doesn't help. Only
  running fewer classes per process does (a `--filter` per step), so keep that in mind before capping the test heap
  (`DOTNET_GCHeapHardLimit`) or moving the tests to a smaller runner.

## API tests

Derive from `ApiFixture` (`Client`, `Api`, `Host`, `ConnectionString`, `OpenDatabaseAsync()`). `Api` is an `ApiClient`: it
sends `ClientHeader` and JSON, and offers `GetAsync`, `GetJsonAsync<T>`, `PostJsonAsync`, `PatchJsonAsync`, `PutJsonAsync`,
`DeleteAsync`. Read an error with `ApiClient.ReadApiErrorAsync(response)` or `(response, status)`: the body must be
`{ code, error }` plus, when a field is at fault, a string `field`. (`WebApi.Tests/Support/ApiErrors.cs` has the same check as an extension method.)

## Acceptance tests

Derive from `SeededAcceptanceTest` or `EmptyAcceptanceTest` (`ShipLean.AcceptanceTests.Harness`). Each class gets a clone, a
host serving the built SPA, a browser context from the process's shared Chromium (closed by `BrowserLifetime`), and a page
per test (`Page`, `Expect(...)`, `BaseUrl`, `DatabaseName`). The fixture fails with build instructions if the bundle is
missing.

- Sign-in is real: the fixture signs in once per user per class (`SignedInAs`, `SignInAsync(email, password)`) and keeps the cookie across tests, because login is rate limited; `EmptyAcceptanceTest` signs nobody in. `SignOutAsync` is for tests that need it. See `auth.md`.
- **Waits** (`Waits`): wait for a locator or a response, never for time. `page.RunAndWaitForApiAsync(path, method, action)`
  runs the action and returns the matching API response.
- **Labels** (`ExpectedLabels`): expected relative-time labels from `DisplayDate` and `TimeProvider.System`, e.g.
  `ExpectedLabels.JustNow()`.
- **Links** (`Links`): a `static partial class`. Each area has a `Links.<Area>.cs` with its routes, built from `Hash`,
  `Query`, `Segment` and `Home`, and keeps it in step with the client's routes.
- **Traces:** a failed test leaves a Playwright trace in `artifacts/playwright-traces/` (open with
  `pnpm exec playwright show-trace <zip>`).
- **Shards:** CI runs acceptance tests by namespace: `ShipLean.AcceptanceTests.Harness`, `.Journeys` and
  `.Lanes.<Area>`. Put a class in the namespace of its folder; a namespace with no shard still runs in the `Other` shard.
- Never run the full acceptance suite locally; run the class you changed. Selectors are `data-testid` only.
- **Page screenshots:** `src/ShipLean.Client/src/pages/screenshots/<slice>.spec.tsx` shoot whole pages (shell + screen) at
  1440×900 in both themes and compare them with the baselines next to them. They are compared only when `CI` is set
  (`ignoreSnapshots` in `playwright-ct.config.ts`), because baselines come from the CI image. Never commit baselines made
  on the host: regenerate with `src/ShipLean.Client/scripts/update-page-shots.sh [spec…]` (a spec path or a bare slice name
  such as `backlogs`), which runs `mcr.microsoft.com/playwright:v1.62.0-resolute` with `CI=true`, `--update-snapshots=all`
  and fixes file ownership; `--verify` compares only, three repeats each, to prove a shot is stable. A spec is a list of
  `PageShot`s passed to `pageShots` (`screenshots/shoot.tsx`): `{ name, route, arrange, ready, act?, shell }`, where `name`
  is the mockup's name, `arrange` registers stubs, `ready` waits on a `data-testid`, `act` opens a dialog or tab, and `shell`
  makes the helper wait for and mask the Live dot. `stubShell(page, overrides?)` (`screenshots/shellStub.ts`) answers the
  shell's reads from the design's sample data (project SHIP "Storefront", Sprints 21–26 with 24 current, five members);
  `ShellOverrides` (`me`, `project`, `projects`, `sprints`, `members`, `preferences`, `instanceName`) replaces any of it.
  Register feature stubs after it: the last registered route wins.

## Registries

Contracts every feature appends to, one file each, found by reflection:

- `IAccessSweepRows` (`ShipLean.TestSupport.AccessSweep`): `IEnumerable<AccessSweepRow> Rows`, where
  `AccessSweepRow(string Method, string RouteTemplate, ProjectRole Minimum, string? Body = null, string? Url = null, bool InstanceScoped = false)`: `RouteTemplate` is the attribute route exactly as mapped (`api/projects/{id:guid}/sprints`); the sweep calls `Url` when given, otherwise the template with every `{…}` replaced by SHIP's id; instance-scoped rows need an instance admin. A concrete class
  with a parameterless constructor (top-level, public or internal; private nested types are skipped) in `ShipLean.WebApi.Tests`, the only assembly whose rows are swept. `AccessSweepRows.All()` collects every implementation, and `WebApi.Tests/Access/` runs every row as a non-member, a member below the minimum and an allowed role (the allowed leg in a fixture of its own, so a destructive row can't remove what the others read; such a row points at a seed entity no other row uses, and a template with a parameter other than the leading project id must set `Url`); a route under `/api` with no row must be `[InstanceAdmin]` or on its allow-list (`auth.md`).
- `Links` partials (above).
- Test ids (below).

## S3 store tests

`S3BlobStore` is tested against a real S3-compatible server, never AWS: [RustFS](https://github.com/rustfs/rustfs) 1.0.3. `S3Server` (TestSupport) uses `SHIPLEAN_TEST_S3` when it is set, otherwise starts one reused `ghcr.io/rustfs/rustfs:1.0.3` Testcontainers container. Each test class gets a bucket of its own. CI takes the container route.

The server has to replace an object atomically, because `BlobStoreContract` reads a key while it is overwritten. versitygw does not: its posix backend reads an object's ETag and Content-Type from extended attributes on the path, then opens the data file separately, while a put strips the live file's attributes before it links the new file ([`GetObject`](https://github.com/versity/versitygw/blob/v1.10.0/backend/posix/posix.go#L6018) and [`PutObjectWithPostFunc`](https://github.com/versity/versitygw/blob/v1.10.0/backend/posix/posix.go#L4732)). A GET racing a PUT could return one version's headers with another's bytes, and CI failed that way ("Expected hash not equal to calculated hash"). RustFS keeps a GET under a namespace read lock until the body is read and commits a put under the write lock (`crates/ecstore/src/set_disk/ops/object.rs`, `get_object_reader` and `put_object`).

Where images can't be pulled, run the same server from its release binary:

```bash
mkdir -p ~/.local/s3test/data && cd ~/.local/s3test
gh release download 1.0.3 -R rustfs/rustfs -p 'rustfs-linux-x86_64-musl-v1.0.3.zip'
python3 -m zipfile -e rustfs-linux-x86_64-musl-v1.0.3.zip . && chmod +x rustfs
RUSTFS_ACCESS_KEY=shiplean RUSTFS_SECRET_KEY=shiplean-secret RUSTFS_ADDRESS=:9000 nohup ./rustfs data > rustfs.log 2>&1 &

export SHIPLEAN_TEST_S3="serviceUrl=http://127.0.0.1:9000;accessKey=shiplean;secretKey=shiplean-secret;forcePathStyle=true"
shiplean-test src/ShipLean.WebApi.Tests --filter "FullyQualifiedName~ShipLean.WebApi.Tests.Storage"
```

## Test ids

Tests find elements by `data-testid` only. Each id lives once in TypeScript and once in C#, and a unit test pins them:

| TypeScript | C# | Export / class |
|---|---|---|
| `src/ShipLean.Client/src/features/<area>/testIds.ts` | `src/ShipLean.TestSupport/TestIds/<Area>TestIds.cs` | `<area>TestIds` / `<Area>TestIds` |
| `src/ShipLean.Client/src/testIds.ts` | `TestIds/AppTestIds.cs` | `appTestIds` / `AppTestIds` |
| `src/ShipLean.Client/src/components/testIds.ts` | `TestIds/ComponentTestIds.cs` | `componentTestIds` / `ComponentTestIds` |

```ts
export const boardTestIds = {
  cardTitle: 'board-card-title',
} as const;
```

```csharp
namespace ShipLean.TestSupport.TestIds;
public static class BoardTestIds
{
    public const string CardTitle = "board-card-title";
}
```

One `key: 'value',` per line, single-quoted values, no computed keys or spreads. The C# const name is the TS key with its
first letter upper-cased and nothing else changed; `<Area>` is the folder name with its first letter upper-cased.
`TestIds_ShouldMatchBetweenClientAndTests` (`ShipLean.UnitTest/Harness`) fails if a TS file has no C# class, a C# class has
no TS file, or the two differ in a key or a value.
