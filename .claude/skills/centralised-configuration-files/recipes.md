# Split recipes

How to split each kind of centralised configuration file into a hub and parts. The steps and the done criteria are in [`SKILL.md`](SKILL.md).

## C# constants class

Make the class `partial`. The hub `UsageEventNames.cs` keeps the comment and the `All` builder. Each area gets a part beside it, `UsageEventNames.Backlog.cs`, holding only that area's constants:

```csharp
namespace ShipLean.Contracts.Usage;

public static partial class UsageEventNames
{
    public const string BacklogBulkWritten = "backlog.bulk_written";
    public const string BacklogSorted = "backlog.sorted";
}
```

All parts must be in the same assembly and namespace, so they live in the hub's folder. Once there are more than a handful, move them into a subfolder named after the class. `Links.cs` and its `Links.<Area>.cs` parts in `src/ShipLean.AcceptanceTests/Harness/` are the working example in this repo. Reflection with `typeof(UsageEventNames).GetFields(...)` sees the fields of every part, so the `All` builders keep working and the hub never lists its parts. Call sites don't change.

## C# map built in one method

For a map like `ActionUsageMap`, which fills a dictionary in one `Build()` method, the class goes `partial` too. Each part holds one private static method per area that adds that area's entries. The hub's `Build()` calls one method per area:

```csharp
// ActionUsageMap.cs (hub)
private static Dictionary<string, ActionUsage> Build()
{
    Dictionary<string, ActionUsage> map = new(StringComparer.Ordinal);
    void Add(ActionUsage usage, params string[] actions) { foreach (var a in actions) map.Add(a, usage); }
    AddAuth(Add);
    AddBacklog(Add);
    AddSprints(Add);
    return map;
}

// ActionUsageMap.Backlog.cs (part)
private static void AddBacklog(Action<ActionUsage, string[]> add)
{
    add(ActionUsage.Track(UsageEventNames.BacklogBulkWritten), ["BacklogWrites.Bulk"]);
    add(ActionUsage.Untracked(ScreenCounts), ["LevelBacklog.Get"]);
}
```

The hub gets one line per area, and that line changes only when an area is added. Shared reason strings (`ScreenCounts`, `Helper`) stay in the hub as `private const`, so every part can see them. Keep the calls explicit rather than scanning the assembly: an area that is never called then shows up as a failing coverage test.

## TypeScript module

TypeScript has no partial classes. Give each area its own module that exports its entries, and have the hub import them and combine them, one line per area. The hub keeps its exported names and types, so importers don't change. For a typed table like `routeDefs` in `routes.ts`, each part checks its own slice against the shared type with `satisfies`:

```ts
// routes/backlog.ts (part)
export const backlogRoutes = {
  storiesBacklog: { /* ... */ },
  featuresBacklog: { /* ... */ },
} satisfies Partial<RouteDefs>;

// routes.ts (hub)
import { backlogRoutes } from './routes/backlog';
import { boardRoutes } from './routes/board';
const routeDefs: RouteDefs = { ...backlogRoutes, ...boardRoutes };
```

The hub's full type annotation keeps a missing route a type error. A spread silently lets a later key override an earlier one with the same name: if nothing checks for that yet, add a unit test that fails when two parts share a key. Keep the parts beside the hub, not in feature folders: the hub importing from features is how import cycles start.

## Mirrored pairs

Some files are mirrored across languages, such as `ScreenIds.cs` and `routes.ts`. Split both sides along the same seam with matching names, so a feature edits one part on each side. Any test that compares the two sides keeps comparing the combined sets.

Test ids are already split one area per file, and `TestIdPin` (`src/ShipLean.UnitTest/Harness/TestIdPin.cs`) pins each `features/<area>/testIds.ts` to its `TestIds/<Area>TestIds.cs`, with no spreads allowed. When one area's pair gets hot, split the area: a new `features/<area>/` folder brings its own pair. Never spread parts into a `testIds.ts`.

## Docs

Use progressive disclosure. The hub page keeps the overview, the rules and the how-to, plus a table with one row per area page:

```markdown
| Area | Page |
|---|---|
| Backlog | [docs/analytics/backlog.md](analytics/backlog.md) |
| Sprints | [docs/analytics/sprints.md](analytics/sprints.md) |
```

Each area page holds that area's entries and the notes that apply only to them.

A table that copies code, such as an event catalogue mirroring `UsageEventNames` and the usage maps, is a cache. Every PR edits it and it still drifts. Prefer deleting it and pointing at the code, which is already the source of truth. When readers need the table (people who don't read C#), generate it from the code with a script or a test that writes the file, and resolve any conflict on it by running the generator again. Never edit it by hand.

## JSON or YAML config

Use a directory of fragments, the `conf.d` pattern: one file per area, loaded and merged at start-up in a fixed order (sorted by file name). Add a test that fails when two fragments define the same key.
