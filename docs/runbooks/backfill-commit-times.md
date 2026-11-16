# Backfilling commit times

Lead time ends at a task's last commit, and a commit link stores its time in `work_item_link.committed_at`. Links made before
`report_task` took `committed_at` have none. The temporary instance-admin MCP tool `backfill_commit_times` fills them. Commits of
readable repositories live in `work_item_commit` and already have a time (`code_commit.authored_at`); only commit links need this.

The tool is temporary and is removed once existing installs are backfilled (see the TODO in `BackfillCommitTimesTools.cs`).

## Who and where

Connect the agent to the install as an instance admin. Anyone else gets `not_found`.

## Steps

1. Call `backfill_commit_times` with no arguments. It returns `missing`: commit links with no time, each `{sha7, url, item}`, 200 a
   page. While `next_cursor` is set, call again with `after` set to it and collect every page.
2. Group the links by repository (the url minus its `/commit/<sha>`). Check out each repository you can read.
3. In each checkout, get the time of each sha with its author date:

   ```sh
   git log --all --format='%H %aI' | grep -E '^(abc1234|bcd2345)'
   # or, for a list of sha7 values: git log --all --format='%H %aI' | awk 'NR==FNR{want[$1]=1; next} substr($1,1,7) in want' shas.txt -
   ```

   Use the full `%H` as `sha` (the first 7 characters are what match) and `%aI` as `committed_at`.
4. Send them in batches of at most 500: `backfill_commit_times` with `commits: [{"sha": "<full sha>", "committed_at": "<%aI>"}]`.
   The reply gives `updated`, `already_set` and `not_found`.
5. A sha missing from `git log` (a rebased or deleted commit, or a repository you cannot read) stays in the listing. Leave it: its link
   keeps no time and lead time falls back to the task's resolve.
6. Call with no arguments again; only the shas you could not find should remain.

## Safe to repeat

A time already set is never changed, so running the steps twice, or while `report_task` is still adding times, does no harm.
