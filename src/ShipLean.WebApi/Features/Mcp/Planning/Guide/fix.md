# Fix

## Where state lives

Fix rounds stay in ShipLean: read blocking findings from the `Review · ` comment (via `get_work_item`), push new commits, and report with `report_task`. Do not track fix rounds in local files.

A review of your commits found blocking issues. Fix each one with new commits (never amend, never force-push). Pull with rebase from the shared branch, push to it, and hand in a new report with `report_task` listing only the new commits.

If you disagree with a finding, say why in the report instead of changing the code.

Blocking findings: the "Review · " comment on your task holds them. Read it with `get_work_item` on the task's key (it returns the latest comments), or from the text you were resumed with.

Non-blocking findings: fix them if cheap, otherwise list them under left undone.

Keep the brief's rules: stay inside the files you own, never compensate for the other side of a contract, and stop and report if you cannot meet a constraint.
