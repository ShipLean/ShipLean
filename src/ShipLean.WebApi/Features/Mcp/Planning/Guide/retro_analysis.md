# Analyse retros in aggregate

Reading one retro at a time does not show which problems keep coming back. People pick themes by hand, so one problem ends up under many names. This method turns a project's repeated retro points into ranked fixes. It is the method used on a year of review comments, applied to retro points: filter, hydrate, rewrite each point as a generic lesson, cluster, merge, check each fix against what is already enforced, then report.

You do the rewriting, embedding, clustering and checking. ShipLean exports the points, counts and stores the result, and makes the action items. ShipLean never changes a repository, and neither does this run: the person decides what to do there.

## When to run

Run it only when asked, or when the plan says to. Nothing prompts you when a sprint or a Feature closes.

Pick one scope, the same way `decide_retro_actions` does:

- a project over a date range: `from` and `to` (yyyy-MM-dd, UTC, inclusive, by the day a point was logged), with `project` when the user is in more than one;
- a sprint: `sprint` as current, next or its name;
- a Feature: `feature`, which covers the Feature's retro and those of its stories and bugs.

Give the same scope to every call below. Points that are already actioned or marked not needed are left out of the run. Points an earlier analysis marked noise, folded or oneOff are left out too, unless you pass `exclude_covered` as false.

## Steps

Work through the steps in order. Keep a table of your own, one row per point, with its id and what you decided; you send it back in step 9.

### Step 0: Export the points

Call `export_retro_points` with the scope. It returns a page of points (50 by default, up to 100 with `limit`) and a `next_cursor`. Pass it back as `cursor` until it is null. The total is in `total_points`.

Each point carries its text, `root_cause`, theme, the item (key, type, title, brief), the Feature, the sprint, the agent, and the item's commits with the paths they changed. Each retro on the page comes once under `retros`, with its failure log (blockers, assumptions disproved, friction). The first page also lists the project's `open_actions`.

Went-well points come with the rest. Set `include_went_well` to false only if the person asked for no went-well patterns.

For a big scope, give each page to a sub-agent for steps 1 to 5 and collect what they return. Brief each one with the rewrite prompt below.

### Step 1: Drop the noise

Mark a point `noise` when it:

- only reports status or chatter ("tests passed", "done");
- is an exact duplicate of another point on the same item, logged by another agent.

Keep the first of a duplicate pair and mark the later ones `noise`.

Went-well points are not noise. Set them aside for step 6 (patterns of kind wentWell).

### Step 2: One point per issue per retro

A went-badly point and its matching improve point in the same retro are one issue. Keep the went-badly point, put the improve text into its rewrite, and mark the improve point `folded` with `folded_into` set to the kept point's id.

### Step 3: Hydrate

Read what each kept point came with: its text, `root_cause`, the retro's failure log, the item and its brief, the Feature's goal, the commits and changed paths, the agent and its role. That is the context the rewrite needs. Do not fetch more unless a point cannot be understood without it.

### Step 4: Keep what is about how work gets done

Keep points about code, tests, tooling, environment, briefs and process. A point that only concerns a product decision on its item is a one-off.

### Step 5: Rewrite and classify

Give each kept point, with its hydrated context, to a model. Use a cheap, fast one for volume. Use this prompt as it is:

```
You are reading one retrospective point from a software delivery run, with its context: the retro's failure log, the work item and its brief, the Feature's goal, the commits and the paths they changed, and the agent that wrote it.

Rewrite the point as a generic lesson that would apply to any work item, not only this one. Name the kind of thing that went wrong and what would have prevented it. Do not use item keys, names, file names or numbers from this item.

Then classify it:
- recurring: a delivery problem that could happen again on other work (code, tests, tooling, environment, briefs, process, how agents work).
- oneOff: a product decision, a specific bug, a question, or chatter that would not repeat.

Answer with JSON only: {"generic": "<the lesson, one or two sentences>", "class": "recurring" or "oneOff", "reason": "<one sentence on why>"}
```

A `oneOff` point gets outcome `oneOff` and keeps its generic text. A recurring point goes on to step 6. Read a sample of the rewrites yourself before you embed all of them: a rewrite that still carries the item's specifics will cluster by item, not by problem.

### Step 6: Embed and cluster

Embed the `generic` text of every recurring problem point with any sentence-embedding model you have, normalised to unit length. Write down the model's name: you send it as `embedding_model`. Use the same model for the whole run. ShipLean returns no vectors; you make them.

Cluster with HDBSCAN. Normalised vectors make Euclidean distance rank the same as cosine, so use the defaults on normalised vectors. Start with a minimum cluster size of 3 and minimum samples of 2:

```python
import numpy as np
from sklearn.cluster import HDBSCAN

vectors = np.asarray(vectors, dtype=float)
vectors /= np.linalg.norm(vectors, axis=1, keepdims=True)
labels = HDBSCAN(min_cluster_size=3, min_samples=2).fit_predict(vectors)
# label -1 is not in any cluster: outcome unclustered
# each other label is a cluster; its centroid is the mean of its vectors, normalised
```

Points with label -1 get outcome `unclustered`. They come back in later exports, because a later run may find their pattern. Points in a cluster get outcome `clustered`.

If almost everything lands in one cluster or almost nothing clusters, change the settings and look again. Say in the report what you used.

Cluster the went-well points on their own, the same way. They never mix with the problem points. A went-well pattern tells the person what is working. It never makes a fix or an action item. Went-well points are never decided, so every run clusters them again.

#### Continuing an earlier analysis

To fit new points into patterns an earlier run found, export with `with_patterns_from` set to `latest`, or to an analysis id. The first page then carries `previous_patterns`, each with its title, lesson, `embedding_model` and centroid. `latest` gives only the newest analysis's patterns.

- Same embedding model as yours: for each new point, find the previous pattern whose centroid has the highest cosine with it. At 0.80 or more, put the point in that pattern: send the pattern with `continues` set to the earlier pattern's id, and the new points as its `point_ids`. Cluster only the points that fit nothing.
- A different model: the vectors are not comparable. Cluster the whole range, and send no `continues`.

Evidence for a continued pattern covers this run's points only.

### Step 7: Merge near-duplicate clusters, then name them

HDBSCAN leaves near-duplicate clusters. Give a model each cluster's title and a handful of its generic lessons, and ask which clusters describe the same problem. Merge those: one pattern, the points of all of them. Check each merge by reading two or three lessons from each side. Do not merge two clusters only because they share a word; merge them when the same fix would stop both.

Name each pattern:

- `title`: the problem in a few plain words ("Tests depend on shared state"), not a theme label.
- `lesson`: one or two sentences that apply to any item.
- `asks`: the specific things to change, each one a short instruction taken from the points' own text and commits. A pattern of 9 points might have 3 asks.

### Step 8: Propose fixes, then check them against what is enforced

For each problem pattern, propose one or more fixes. Place each in the highest tier that can stop the problem (see Tiers). Give it a `fix_type`.

Then check each fix against what already exists. A fix is only "already enforced" if something actually stops the problem, so read what the thing does. Do not go by its name or by what a description says.

- Analyzers, lint rules and CI steps in the repository: open the rule or the step and read what it matches and what it fails on.
- Skills, `planning_guide` parts, the project instruction files, and agent prompts: read the lines that bear on the problem and decide whether an agent following them would avoid it.

Mark each fix:

- `fully`: it already stops the problem. Put what stops it in `enforced_by`.
- `partly`: it stops some cases. Say in `enforced_by` what it covers and in `reason` what it misses.
- `notCaught`: nothing stops it.
- `discarded`: you paired the fix with a rule or text that only sounds right and does not do it, or the fix would not stop the problem. Say why in `reason`. Count these: the person wants to see the pairings that were thrown out.

If an open action item from `open_actions` already covers the fix, send its key as `existing`, and the points are added to it.

### Step 9: Write the report, then record

Write the report using the template under Report. Then call `record_retro_analysis`:

1. Send the scope, `exclude_covered` as you exported, `embedding_model`, `rewrite_model` if you want it kept, `points` (every point you looked at, with `outcome`, `folded_into` for folded points, and `generic` for clustered, unclustered and oneOff ones), `patterns` (with `ref`, `kind`, `title`, `lesson`, `asks`, `point_ids`, an optional `centroid` and an optional `continues`), `fixes` (with `pattern_ref`, `title`, `fix_type`, `enforcement`, `enforced_by`, `reason`, `existing`) and `report_markdown`. Leave `plan` out. That is a preview.
2. Read the preview. ShipLean has worked out the funnel and each pattern's evidence from its own data, ranked the patterns, and listed the action items it would create or add to. Check the numbers against yours. Points in scope that you did not send are listed in `warnings`: go back and look at them, or say in the report why you left them. Use ShipLean's numbers in your report, not your own.
3. Apply by calling it again with exactly the same arguments plus the `plan` the preview returned. Changed arguments are refused.

When the arguments are large, write them to a JSON file, call `create_payload_upload` with tool `record_retro_analysis`, and send the file with the `curl -T` it returns. Preview with `payload` in place of the arguments, then apply with the same `payload` plus `plan`.

The apply creates the action items first, then stores the analysis. A point someone decided since the preview is skipped and listed in `skipped`; the analysis is still stored. Read `created` for the new action items' keys and `analysis_id` for the stored analysis. Read an analysis back with `get_retro_analysis`, with its `id`, or without one for the project's newest ten.

One action item per problem pattern. Within a pattern, ShipLean takes the top-ranked fix that is `notCaught` or `partly` and makes it the action item. It carries all of the pattern's points. The pattern's other fixes are stored against the same action item and shown in the report and in `get_retro_analysis`; they do not get items of their own. So rank a pattern's fixes on purpose: the fix you want done first goes first, and the order you send them in breaks ties. A `fully` or `discarded` fix never makes an action item. There is no limit on how many new action items one run makes; the preview is the human check, so look at it.

## Tiers

Each fix goes in the most deterministic tier that can really prevent the problem, at the earliest feedback point. A compile error beats a failing test, and a failing test beats a prompt line. Drop to a lower tier only when a higher one cannot prevent the problem. Ask of each fix: could a higher tier stop this? If yes, propose that instead.

In order, with the `fix_type` each one uses:

1. Fix the broken thing: flaky tests, tests that cannot run, environment and host problems, broken tooling. `fix`. Priority 1.
2. Type system, and code and project structure: change types, module boundaries and layout so the mistake cannot be written. `structure`. Priority 2.
3. Static code analysis: add or change an analyzer or lint rule. `analyzer`. Priority 3.
4. Test automation: add a test or check that flags the problem to agents before they repeat it. `test`. Priority 4.
5. Agent prompt and instructions: change a skill, a guide part or a prompt. `skill` or `guide`. Priority 4.

A fix that changes the product itself, or that fits none of these, is `product` or `other`. They sit outside the tiers and rank after them.

The tier sets the order and the priority of the action items. Within a tier, the larger, more recent and more widespread pattern goes first: ShipLean ranks by tier, then the pattern's point count, then its latest point, then how many items it spans. Each action item gets the tag fix: followed by its type.

## Report

Return the report to the person who asked, and send it as `report_markdown`. Plain words, no more than it takes. Use this shape:

```
# Retro analysis: <scope>, <date>

## Funnel
Points in -> noise -> folded -> one-offs -> generic -> clustered (percent) -> patterns -> fixes proposed -> already enforced (fully, partly) -> discarded -> action items (new, added).
Embedding model, rewrite model and clustering settings.

## Patterns, ranked
For each problem pattern, in rank order: title, lesson, evidence (points, items, Features, sprints, agents, first and last logged), the specific asks, and the action item it became.

## Fixes by tier
Fix, structure, analyzer, test, skill or guide, each with its fixes: what, which pattern, and its enforcement.

## Already enforced
Fixes marked fully or partly: what enforces them and what it misses.

## Discarded pairings
Each fix you threw out, the rule or text it was paired with, and why it does not do the job.

## What is working
The went-well patterns: what keeps going right, with evidence. Keep doing these.

## Left out
Points in scope you did not send, unclustered points, and anything the person should look at.
```

Finish by telling the person the headline numbers, the action items created and where to read the analysis. Nothing in any repository has changed.
