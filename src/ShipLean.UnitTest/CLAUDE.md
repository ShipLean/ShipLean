# ShipLean.UnitTest

Use this project sparingly. It is the exception, not the default.

Most tests belong elsewhere, in this order of preference:

1. **`ShipLean.AcceptanceTests`**: Playwright against the real BFF and the built SPA bundle. Best for anything a user can see or do.
2. **`ShipLean.WebApi.Tests`**: black-box tests through `WebApplicationFactory` and the HTTP surface. Best for API behaviour, status codes, persistence and auth.
3. **`ShipLean.UnitTest`** (here): last resort.

## What belongs here

Only pure logic that is awkward to reach through the layers above: helper methods, formatters, parsers, calculations with many edge cases (`ShipLean.Core`, and pure classes in `ShipLean.Application`). No database, no DI container, no HTTP. Reading repository files through `RepoPaths` (fixtures) is fine.

One other kind of test lives here because it needs no host:

- **Architecture and repository guard rails** (`Architecture/`): reflection over the production assemblies (feature isolation, no `ShipLean.TestSupport` or EF Core references, one registration per contract interface) and scans of the repository itself (no hard deletes, no name from another product).

That is why this project references `ShipLean.Core`, `ShipLean.Contracts`, `ShipLean.Application`, `ShipLean.WebApi` and `ShipLean.TestSupport`. Anything that needs a database or the running app goes in `ShipLean.WebApi.Tests`.

## What does not belong here

- Controllers, services, middleware or anything wired through DI: test it through the HTTP surface in `ShipLean.WebApi.Tests`.
- Mocks of our own types. If a test needs one, it is at the wrong level.
- A test per method for coverage's sake. If an acceptance or API test already pins the behaviour, do not duplicate it here.

Naming is `Method_When_Should`, as everywhere (`.claude/skills/csharp-testing-strategy/`). Never weaken or delete an existing test (`.claude/skills/preserve-existing-tests/`).
