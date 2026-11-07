---
name: centralised-configuration-files
description: How to keep a file that every feature adds an entry to from becoming a merge-conflict hotspot, by splitting it into a hub and per-area part files. Use when adding an entry to a centralised configuration file (an event catalogue, constants class, route table, test-id class, usage map, or a doc table listing every event or route), when a merge conflict lands in one, or when creating a new one.
---

# Centralised configuration files

A **centralised configuration file** is one file that every feature adds an entry to: an event catalogue, a constants class, a route table, a test-id class, a map keyed by controller action, a doc table that lists every event. While the repo is small it is the right call: there is one place to look, and with few authors nobody collides. As the repo grows and agents work in parallel, every PR appends to the same region of the same file and it becomes a **hotspot**, the file most PRs conflict on. Resolving it is nearly always "keep both sides", but each one still costs a rebase, a CI run and sometimes a bad merge.

The cure keeps the one place to look but stops every change landing in it. A small **hub** holds the rules and the shared machinery, and one **part** file per area holds the entries. The hub changes only when an area is added, which is rare. A part changes when an entry is added, and only PRs in that area touch it.

The pattern comes from an `ExperimentIds.cs` that held a string constant for every running A/B experiment. It conflicted on almost every PR until it became a `partial class` with one part per team.

## Spot one

- **Churn.** List the files that PRs edit most often:
  `git log origin/main --no-merges --format= --name-only | grep -vE '\.png$|snapshots' | sort | uniq -c | sort -rn | head -30`
  A file near the top is a candidate when PRs edit it to add an entry, not to change behaviour.
- **Shape.** A flat list of peer entries, one per feature, mostly appended at the end. Its conflicts are two PRs adding lines next to each other.
- **A hand-kept copy of code.** A doc that says "update this table in the same change", or calls itself "a copy of the code that can go stale". Every PR edits it, and it still drifts.

## Add an entry

- **Already split:** add the entry to your area's part. If your area has no part yet, create one; the hub's top comment says how.
- **Not split yet:** add the entry with its area's group (or in sorted position), never at the end of the file, so parallel PRs don't append to the same line. Name the hotspot in your item's retrospective. Do not split the file inside your feature PR: the split moves every entry and conflicts with every open PR that touches the file, so it ships on its own.

## Split one

1. **Use its own PR, branched from main, with nothing else in it.** Run `gh pr list` and note the open PRs that touch the file. Each will conflict once, and resolves by moving its new entry into the right part. Merge the split quickly so that cost is paid only once.
2. **Cut along the seam the repo already uses**: the feature folder names (`features/backlog`, `Features/Sprints`) or the prefix the entries already share (`backlog.*`, `sprint_demo.*`). Give each area its own part, however small. A shared "misc" part becomes the next hotspot.
3. **Move entries without changing them.** Every key, value and order-sensitive behaviour stays the same. The existing coverage tests (such as `UsageEventNamesTests` and `ActionUsageMapTests`) pass unchanged; that is the proof the move lost nothing. If the file has no such test, add one that checks every key is unique and every key from before the split is still there.
4. **Keep the hub to the rules and the machinery**: the explanatory comment, the `All` builders and helpers, the shared constants, and for docs one pointer per area. Its top comment says where a new entry goes and how to add an area.
5. **Retarget the instructions.** Every doc that tells agents to edit the old file, such as an "add a tracked action" checklist in `docs/`, `AGENTS.md` or a skill, now names the parts.

Recipes for each kind of file (C# constants and maps, TypeScript modules, mirrored C# and TypeScript pairs, docs, JSON or YAML config) are in [`recipes.md`](recipes.md).

**Done when** the hub has no per-entry lines, only per-area ones; every entry from before the split is still there with the same key and value, shown by the coverage tests passing unchanged; and no instruction in the repo still points at the old single file for new entries.
