## Report

Hand in the report with `report_task` on your task, once per stop, whether you finished or not. Fill the arguments like this:

- `item`: your task's key.
- `status`: done, blocked or partial.
- `commits`: your commits in order, each with its `sha`, `subject` and `url`.
- `pr_url`: the pull request, when there is one.
- `tests_run`: one entry per test command, with its pass and fail counts and how long it took. Summarise the inner loop in one entry.
- `acceptance_tests`: the acceptance tests that define done, each with pass or fail.
- `config_files_changed`: every configuration file you changed and why, or leave out.
- `red_tests`: each red test with the task that owns it, or leave out.
- `context_used_k`: about how much context you used, in thousands of tokens.
- `public_surface`: the types, tools, endpoints and signatures you added that others build on. Signatures only.
- `decisions`: anything a later task would otherwise have to rediscover.
- `gotchas`: what cost you time, and what not to touch.
- `left_undone`: each item with why. Each one becomes an orphan task, so name it so it can be planned.
- `temporary_behaviour`: each file, what its TODO stands in for, and who replaces it.
- `constraint_not_met`: a constraint of the brief you could not meet, and why.

Rules:

- Status done means every acceptance test in your definition of done passes. A task whose own definition of done appears in its left-undone list is not done.
- Status blocked unassigns the task and returns it to the team with your reason, so say precisely what blocks you and what you tried.
- Status partial leaves the task with you; list exactly what is left so a fresh agent can continue.
- Report what happened, not what you hoped. A red test you did not mention is a defect in the report.
- After a review, report again with only the new commits.
