-- Metric history for SHIP's Sprints 19-23 and the Sprint 24 burndown. The semantics this script seeds to:
--   committed = story points of Stories and Bugs whose sprint was this sprint at the sprint's start (start date 00:00 UTC)
--   completed = points of Stories and Bugs whose state category became Completed while their sprint was this sprint,
--               between its start and end, whenever they were added to it
--   task points never count
--   burndown  = remaining (committed + added - completed) points of Stories and Bugs at the end of each working day
-- Sprints 19-23 commit/complete 18/16, 22/19, 20/20, 24/22, 23/21; Sprint 24 commits 21 and burns down 21 -> 18 -> 16
-- over its first three working days (SHIP-150 closes on day 2, SHIP-138 on day 3).
-- Every figure derives from `revision` rows alone. Leftovers (committed, not completed) move to the next sprint by a revision at
-- 17:00 UTC on the last working day before that sprint's start date. SHIP-138 was carried from Sprint 23 into Sprint 24.
-- All times are working days (Mon-Fri) computed from the sprint dates, so nothing is a date literal.
-- Existing rows are touched only here: SHIP-118, 120, 124, 126, 131, 138 and 150 get a real history (their revision 1 becomes
-- the creation, written by the same SQL as 0005), and SHIP-138's close moves to 15:00 UTC on Sprint 24's working day 3.
-- New history items use key numbers below 90 and do not change project.next_item_number.

-- Working days (Mon-Fri) of every SHIP sprint, numbered from 1.
CREATE TEMP TABLE history_day AS
SELECT sp.id AS sprint_id, sp.start_date, row_number() OVER (PARTITION BY sp.id ORDER BY d.day) AS n, d.day
FROM sprint sp
JOIN project ON project.id = sp.project_id AND project.key = 'SHIP'
CROSS JOIN LATERAL (
    SELECT g::date AS day
    FROM generate_series(sp.start_date::timestamp, sp.end_date::timestamp, interval '1 day') AS g
    WHERE extract(isodow FROM g) <= 5
) AS d;

-- SHIP-138 closes at 15:00 UTC on Sprint 24's working day 3 (it was closed on day 2 like SHIP-150).
UPDATE work_item
SET state_changed_at = ((SELECT day FROM history_day JOIN sprint ON sprint.id = history_day.sprint_id
                          WHERE sprint.name = 'Sprint 24' AND history_day.n = 3)::timestamp + interval '15 hours') AT TIME ZONE 'utc',
    changed_at = ((SELECT day FROM history_day JOIN sprint ON sprint.id = history_day.sprint_id
                    WHERE sprint.name = 'Sprint 24' AND history_day.n = 3)::timestamp + interval '15 hours') AT TIME ZONE 'utc'
WHERE key = 'SHIP-138';

-- The history items. s0 = the sprint the item was planned into, s1 = the sprint it closed in (differs for a leftover).
-- added_day: working day of s0 when it was added after the start (NULL = planned before the start). active_day / close_day:
-- working days of s0 / s1. An existing item (existing = true) keeps its row and its close time; NULL columns come from the row.
CREATE TEMP TABLE history_item AS
SELECT v.number, v.type, v.title, v.points, v.s0, coalesce(v.s1, v.s0) AS s1, v.added_day, v.active_day, v.close_day,
       v.assignee, v.parent_number, v.existing
FROM (VALUES
  -- Sprint 19: commits 18, completes 16; SHIP-34 (2) is carried
  (30, 'story', 'Cart persistence across sessions', 5, 19, NULL::int, NULL::int, 2, 6, 'priya', NULL::int, false),
  (31, 'story', 'Shipping estimate on the cart page', 3, 19, NULL, NULL, 2, 4, 'tom', NULL, false),
  (32, 'story', 'Order history list', 5, 19, NULL, NULL, 2, 9, 'mei', NULL, false),
  (33, 'bug', 'Cart badge shows a stale count', 3, 19, NULL, NULL, 2, 7, 'sam', NULL, false),
  (34, 'story', 'Promo code field validation', 2, 19, 20, NULL, 3, 3, 'ana', NULL, false),
  -- Sprint 20: commits 22 (20 + the carried 2), completes 19 (one bug added on day 4); SHIP-39 (5) is carried
  (35, 'story', 'Guest address book', 5, 20, NULL, NULL, 2, 8, 'priya', NULL, false),
  (36, 'story', 'Wishlist sharing link', 5, 20, NULL, NULL, 2, 10, 'tom', NULL, false),
  (37, 'story', 'Currency selector', 3, 20, NULL, NULL, 2, 5, 'mei', NULL, false),
  (38, 'bug', 'Currency selector resets on reload', 2, 20, NULL, NULL, 3, 9, 'sam', NULL, false),
  (39, 'story', 'Refund request form', 5, 20, 21, NULL, 4, 4, 'ana', NULL, false),
  (40, 'bug', 'Tax rounding on invoices', 2, 20, NULL, 4, NULL, 8, 'priya', NULL, false),
  -- Sprint 21: commits 20 (15 + the carried 5), completes 20
  (41, 'story', 'Report: revenue by day', 5, 21, NULL, NULL, 2, 7, 'tom', NULL, false),
  (42, 'story', 'Report: CSV download', 5, 21, NULL, NULL, 2, 9, 'mei', NULL, false),
  (43, 'story', 'Report date range picker', 3, 21, NULL, NULL, 2, 6, 'sam', NULL, false),
  (44, 'story', 'Report empty state', 2, 21, NULL, NULL, 3, 5, 'ana', NULL, false),
  -- Sprint 22: commits 24 (SHIP-118 and 120 are 8 of it), completes 22 (one bug added on day 3); SHIP-49 (5) is carried.
  -- SHIP-45 is a Task with points: it never counts.
  (45, 'task', 'Instrument checkout timings', 3, 22, NULL, NULL, 2, 5, 'priya', 46, false),
  (46, 'story', 'Lazy-load the payment form', 5, 22, NULL, NULL, 2, 8, 'priya', NULL, false),
  (47, 'story', 'Skeleton loaders for the cart', 3, 22, NULL, NULL, 2, 6, 'tom', NULL, false),
  (48, 'bug', 'Slow price lookup on large carts', 3, 22, NULL, NULL, 2, 9, 'mei', NULL, false),
  (49, 'story', 'Saved address picker', 5, 22, 23, NULL, 3, 5, 'sam', NULL, false),
  (50, 'bug', 'Checkout fails on an empty cart', 3, 22, NULL, 3, NULL, 7, 'ana', NULL, false),
  -- Sprint 23: commits 23 (SHIP-49 carried in; SHIP-124, 126, 131 are 8 of it), completes 21; SHIP-138 (2) is carried
  (51, 'story', 'Coupon stacking rules', 5, 23, NULL, NULL, 2, 8, 'priya', NULL, false),
  (52, 'story', 'Coupon expiry banner', 3, 23, NULL, NULL, 2, 6, 'tom', NULL, false),
  -- Existing items that get a history
  (118, NULL, NULL, NULL, 22, NULL, NULL, 2, NULL, NULL, NULL, true),
  (120, NULL, NULL, NULL, 22, NULL, NULL, 2, NULL, NULL, NULL, true),
  (124, NULL, NULL, NULL, 23, NULL, NULL, 2, NULL, NULL, NULL, true),
  (126, NULL, NULL, NULL, 23, NULL, NULL, 2, NULL, NULL, NULL, true),
  (131, NULL, NULL, NULL, 23, NULL, NULL, 2, NULL, NULL, NULL, true),
  (138, NULL, NULL, NULL, 23, 24, NULL, 2, NULL, NULL, NULL, true),
  (150, NULL, NULL, NULL, 24, NULL, NULL, 1, NULL, NULL, NULL, true)
) AS v (number, type, title, points, s0, s1, added_day, active_day, close_day, assignee, parent_number, existing);

-- New work items, in their final (Closed) state. Revision numbers and change times are set below, from the revisions.
INSERT INTO work_item (id, project_id, number, key, type, title, state_id, assigned_to, sprint_id, priority, story_points,
                       backlog_order, parent_id, parent_type, tags, board_column_id, board_column_done, column_order,
                       revision, created_by, created_at, changed_by, changed_at, state_changed_at)
SELECT format('00000005-0000-4000-8001-%s', lpad(h.number::text, 12, '0'))::uuid,
       project.id,
       h.number,
       'SHIP-' || h.number,
       h.type,
       h.title,
       closed.id,
       person.id,
       sprint.id,
       2,
       h.points,
       lpad((30000 + h.number * 10)::text, 8, '0'),
       CASE WHEN h.parent_number IS NOT NULL THEN format('00000005-0000-4000-8001-%s', lpad(h.parent_number::text, 12, '0'))::uuid END,
       CASE WHEN h.parent_number IS NOT NULL THEN coalesce(parent_item.type, 'feature') END,
       '{}'::text[],
       CASE WHEN h.type = 'task' THEN NULL ELSE 'col-done' END,
       false,
       CASE WHEN h.type = 'task' THEN NULL ELSE lpad((30000 + h.number * 10)::text, 8, '0') END,
       1,
       creator.id,
       created.at,
       person.id,
       created.at,
       created.at
FROM history_item h
JOIN project ON project.key = 'SHIP'
JOIN state closed ON closed.project_id = project.id AND closed.type = h.type AND closed.name = 'Closed'
JOIN app_user person ON lower(split_part(person.name, ' ', 1)) = h.assignee
JOIN sprint ON sprint.project_id = project.id AND sprint.name = 'Sprint ' || h.s1
JOIN sprint first_sprint ON first_sprint.project_id = project.id AND first_sprint.name = 'Sprint 19'
JOIN app_user creator ON creator.id = '00000001-0000-4000-8000-000000000002'
LEFT JOIN history_item parent_item ON parent_item.number = h.parent_number AND NOT parent_item.existing
CROSS JOIN LATERAL (
    SELECT ((first_sprint.start_date::timestamp + interval '9 hours') - interval '14 days' + h.number * interval '1 minute') AT TIME ZONE 'utc' AS at
) AS created
WHERE NOT h.existing;

-- Revision 1 of every history item: the creation at created_at with the initial values (state New, no sprint), written like
-- 0005 writes it, in field-ids order with snapshot labels.
CREATE TEMP TABLE history_rev1 AS
SELECT w.id AS work_item_id, h.existing, w.created_at AS changed_at,
       (SELECT jsonb_agg(jsonb_build_object('field', f.field, 'from', f.from_value, 'to', f.to_value,
                                            'fromLabel', NULL::text, 'toLabel', f.to_label) ORDER BY f.ord)
        FROM (VALUES
            (1, 'title', NULL::jsonb, to_jsonb(w.title), NULL::text, true),
            (2, 'type', NULL, to_jsonb(w.type), NULL, true),
            (3, 'state', NULL, to_jsonb(initial.id), initial.name, true),
            (4, 'assignee', NULL, to_jsonb(w.assigned_to), person.name, w.assigned_to IS NOT NULL),
            (5, 'priority', NULL, to_jsonb(w.priority), NULL, true),
            (6, 'points', NULL, to_jsonb(trim_scale(w.story_points)), NULL, w.story_points IS NOT NULL),
            (7, 'tags', '[]'::jsonb, to_jsonb(w.tags), NULL, cardinality(w.tags) > 0),
            (9, 'parent', NULL, to_jsonb(w.parent_id), parent.title, w.parent_id IS NOT NULL)
        ) AS f (ord, field, from_value, to_value, to_label, included)
        WHERE f.included) AS changes
FROM history_item h
JOIN project ON project.key = 'SHIP'
JOIN work_item w ON w.project_id = project.id AND w.number = h.number
JOIN state initial ON initial.project_id = project.id AND initial.type = w.type AND initial.name = 'New'
LEFT JOIN app_user person ON person.id = w.assigned_to
LEFT JOIN work_item parent ON parent.id = w.parent_id;

INSERT INTO revision (work_item_id, rev, changed_by, changed_at, changes)
SELECT r.work_item_id, 1, '00000001-0000-4000-8000-000000000002', r.changed_at, r.changes
FROM history_rev1 r
WHERE NOT r.existing;

UPDATE revision
SET changed_at = r.changed_at, changes = r.changes
FROM history_rev1 r
WHERE r.existing AND revision.work_item_id = r.work_item_id AND revision.rev = 1;

-- Revisions 2 and later: the sprint is set (at planning, 11:00 UTC on the last working day before the sprint starts, or on the
-- day the item was added), the item goes Active, a leftover moves to the next sprint (17:00 UTC on the last working day before
-- that sprint starts), the item closes. Rev numbers follow the times.
CREATE TEMP TABLE history_event AS
SELECT e.number, e.kind, e.at, e.changed_by, e.changes
FROM (
    SELECT h.number, 'sprint' AS kind,
           ((CASE WHEN h.added_day IS NULL
                  THEN sp0.start_date - CASE WHEN extract(isodow FROM sp0.start_date) = 1 THEN 3 ELSE 1 END
                  ELSE (SELECT d.day FROM history_day d WHERE d.sprint_id = sp0.id AND d.n = h.added_day) END)::timestamp
             + CASE WHEN h.added_day IS NULL THEN interval '11 hours' ELSE interval '10 hours' END
             + h.number * interval '1 minute') AT TIME ZONE 'utc' AS at,
           '00000001-0000-4000-8000-000000000002'::uuid AS changed_by,
           jsonb_build_array(jsonb_build_object('field', 'sprint', 'from', NULL::text, 'to', sp0.id,
                                                'fromLabel', NULL::text, 'toLabel', sp0.name)) AS changes
    FROM history_item h
    JOIN project ON project.key = 'SHIP'
    JOIN sprint sp0 ON sp0.project_id = project.id AND sp0.name = 'Sprint ' || h.s0
    UNION ALL
    SELECT h.number, 'active',
           ((SELECT d.day FROM history_day d WHERE d.sprint_id = sp0.id AND d.n = coalesce(h.added_day, h.active_day))::timestamp
             + CASE WHEN h.added_day IS NULL THEN interval '10 hours' ELSE interval '14 hours' END
             + h.number * interval '1 minute') AT TIME ZONE 'utc',
           w.assigned_to,
           jsonb_build_array(jsonb_build_object('field', 'state', 'from', to_jsonb(st_new.id), 'to', to_jsonb(st_active.id),
                                                'fromLabel', st_new.name, 'toLabel', st_active.name))
    FROM history_item h
    JOIN project ON project.key = 'SHIP'
    JOIN work_item w ON w.project_id = project.id AND w.number = h.number
    JOIN sprint sp0 ON sp0.project_id = project.id AND sp0.name = 'Sprint ' || h.s0
    JOIN state st_new ON st_new.project_id = project.id AND st_new.type = w.type AND st_new.name = 'New'
    JOIN state st_active ON st_active.project_id = project.id AND st_active.type = w.type AND st_active.name = 'Active'
    UNION ALL
    SELECT h.number, 'carry',
           ((sp1.start_date - CASE WHEN extract(isodow FROM sp1.start_date) = 1 THEN 3 ELSE 1 END)::timestamp
             + interval '17 hours') AT TIME ZONE 'utc',
           '00000001-0000-4000-8000-000000000002'::uuid,
           jsonb_build_array(jsonb_build_object('field', 'sprint', 'from', to_jsonb(sp0.id), 'to', to_jsonb(sp1.id),
                                                'fromLabel', sp0.name, 'toLabel', sp1.name))
    FROM history_item h
    JOIN project ON project.key = 'SHIP'
    JOIN sprint sp0 ON sp0.project_id = project.id AND sp0.name = 'Sprint ' || h.s0
    JOIN sprint sp1 ON sp1.project_id = project.id AND sp1.name = 'Sprint ' || h.s1
    WHERE h.s1 <> h.s0
    UNION ALL
    SELECT h.number, 'closed',
           CASE WHEN h.existing THEN w.state_changed_at
                ELSE ((SELECT d.day FROM history_day d WHERE d.sprint_id = sp1.id AND d.n = h.close_day)::timestamp
                      + interval '14 hours' + h.number * interval '1 minute') AT TIME ZONE 'utc' END,
           w.assigned_to,
           jsonb_build_array(jsonb_build_object('field', 'state', 'from', to_jsonb(st_active.id), 'to', to_jsonb(st_closed.id),
                                                'fromLabel', st_active.name, 'toLabel', st_closed.name))
    FROM history_item h
    JOIN project ON project.key = 'SHIP'
    JOIN work_item w ON w.project_id = project.id AND w.number = h.number
    JOIN sprint sp1 ON sp1.project_id = project.id AND sp1.name = 'Sprint ' || h.s1
    JOIN state st_active ON st_active.project_id = project.id AND st_active.type = w.type AND st_active.name = 'Active'
    JOIN state st_closed ON st_closed.project_id = project.id AND st_closed.type = w.type AND st_closed.name = 'Closed'
) AS e;

INSERT INTO revision (work_item_id, rev, changed_by, changed_at, changes)
SELECT w.id, row_number() OVER (PARTITION BY e.number ORDER BY e.at)::int + 1, e.changed_by, e.at, e.changes
FROM history_event e
JOIN work_item w ON w.project_id = (SELECT id FROM project WHERE key = 'SHIP') AND w.number = e.number;

-- Keep the work item row in step with its latest revision (and state_changed_at with the latest state change).
UPDATE work_item
SET revision = last.rev, changed_at = last.at, changed_by = last.changed_by, state_changed_at = last.state_at
FROM (
    SELECT r.work_item_id, max(r.rev) AS rev, (array_agg(r.changed_at ORDER BY r.rev DESC))[1] AS at,
           (array_agg(r.changed_by ORDER BY r.rev DESC))[1] AS changed_by,
           max(r.changed_at) FILTER (WHERE EXISTS (SELECT 1 FROM jsonb_array_elements(r.changes) c WHERE c ->> 'field' = 'state')) AS state_at
    FROM revision r
    WHERE r.work_item_id IN (SELECT w2.id FROM work_item w2 JOIN history_item h ON w2.number = h.number
                             WHERE w2.project_id = (SELECT id FROM project WHERE key = 'SHIP'))
    GROUP BY r.work_item_id
) AS last
WHERE work_item.id = last.work_item_id;

DROP TABLE history_event;
DROP TABLE history_rev1;
DROP TABLE history_item;
DROP TABLE history_day;
