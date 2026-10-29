-- Accounts in every status, and the first-run projects.
-- Users (ids pinned in me.json): Ravi (oneTimePassword), Dana (noPassword, created by the MOB import), Lee (disabled), Jo (locked),
-- Kai (active, in no project) and the Jira import attribution user (disabled, no password, in no project).
-- Projects: ARCH "Legacy Billing" (its only admin, Lee, is disabled: no active admin) and EMPT "Research" (empty, Joel administers).
-- Dev passwords are in SeedIds.Passwords (<first>-dev-password); Dana and Jira import have none.

INSERT INTO app_user (id, email, name, initials, avatar_colour, password_hash, must_change_password, security_stamp, failed_sign_in_count, locked_until, is_admin, disabled_at, created_at)
VALUES
  ('00000001-0000-4000-8000-000000000007', 'ravi@shiplean.local', 'Ravi Menon', 'RM', '#5a5fc9', 'AQAAAAIAAYagAAAAEDVWNJb0KgwuS0yGc9pWzlouTUSFAW7lU+O6foTDHSVWfNxyXZOeNjgNVwu6iym5ng==', true, 'seed-stamp-ravi', 0, NULL, false, NULL, ((now() AT TIME ZONE 'utc')::date - 30)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000009', 'lee@shiplean.local', 'Lee Park', 'LP', '#3f7d5a', 'AQAAAAIAAYagAAAAEKIkd+xk1pgaSLFcFlkGBFuzIzemA0on1Kj25iwZoJVEoYHClMCLuCdQ17CqscFgZA==', false, 'seed-stamp-lee', 0, NULL, false, ((now() AT TIME ZONE 'utc')::date - 55)::timestamp AT TIME ZONE 'utc', ((now() AT TIME ZONE 'utc')::date - 90)::timestamp AT TIME ZONE 'utc'),
  -- Locked until the end of tomorrow (UTC), not relative to now(): the test-database template is cached per UTC day.
  ('00000001-0000-4000-8000-000000000010', 'jo@shiplean.local', 'Jo Becker', 'JB', '#a3416b', 'AQAAAAIAAYagAAAAEFJkt4D6CkGq9IFloveYxGxFX2hfPiKTjIeO/L+nOaexP+XwZQbVJlhJmEfXLlB6lA==', false, 'seed-stamp-jo', 10, ((now() AT TIME ZONE 'utc')::date + 2)::timestamp AT TIME ZONE 'utc', false, NULL, ((now() AT TIME ZONE 'utc')::date - 60)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000011', 'kai@shiplean.local', 'Kai Moreno', 'KM', '#52627c', 'AQAAAAIAAYagAAAAEHncVSa8jSiNPyCCUxP0WXQcpIk/+LnHG6k/kwuBJGA8ZmjZHZfNCFGYc2O+a5r7hA==', false, 'seed-stamp-kai', 0, NULL, false, NULL, ((now() AT TIME ZONE 'utc')::date - 5)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000012', 'jira-import@shiplean.local', 'Jira import', 'JI', '#52627c', NULL, false, 'seed-stamp-jira-import', 0, NULL, false, ((now() AT TIME ZONE 'utc')::date - 2)::timestamp AT TIME ZONE 'utc', ((now() AT TIME ZONE 'utc')::date - 2)::timestamp AT TIME ZONE 'utc');

-- Dana was created by the MOB import, so her created_at is the seeded import job's start.
INSERT INTO app_user (id, email, name, initials, avatar_colour, password_hash, must_change_password, security_stamp, is_admin, created_at)
SELECT '00000001-0000-4000-8000-000000000008', 'dana@shiplean.local', 'Dana Whitfield', 'DW', '#8a5a2b', NULL, false, 'seed-stamp-dana', false, import_job.started_at
FROM import_job WHERE import_job.id = '00000006-0000-4000-8000-000000000001';

INSERT INTO project (id, key, name, description, next_item_number, created_by, created_at)
VALUES
  ('00000002-0000-4000-8000-000000000007', 'ARCH', 'Legacy Billing', 'The retired billing system, kept for reference. Its only administrator has left.', 6, '00000001-0000-4000-8000-000000000009', ((now() AT TIME ZONE 'utc')::date - 120)::timestamp AT TIME ZONE 'utc'),
  ('00000002-0000-4000-8000-000000000008', 'EMPT', 'Research', 'A new project with nothing in it yet.', 1, '0199f000-0000-7000-8000-000000000001', ((now() AT TIME ZONE 'utc')::date - 3)::timestamp AT TIME ZONE 'utc');

INSERT INTO project_member (id, project_id, user_id, role)
VALUES
  ('00000009-0000-4000-8002-000000000004', '00000002-0000-4000-8000-000000000002', '00000001-0000-4000-8000-000000000007', 'member'),
  ('00000009-0000-4000-8003-000000000004', '00000002-0000-4000-8000-000000000003', '00000001-0000-4000-8000-000000000008', 'member'),
  ('00000009-0000-4000-8003-000000000005', '00000002-0000-4000-8000-000000000003', '00000001-0000-4000-8000-000000000010', 'member'),
  ('00000009-0000-4000-8005-000000000004', '00000002-0000-4000-8000-000000000005', '00000001-0000-4000-8000-000000000009', 'member'),
  ('00000009-0000-4000-8006-000000000004', '00000002-0000-4000-8000-000000000006', '00000001-0000-4000-8000-000000000008', 'member'),
  ('00000009-0000-4000-8007-000000000001', '00000002-0000-4000-8000-000000000007', '00000001-0000-4000-8000-000000000009', 'admin'),
  ('00000009-0000-4000-8007-000000000002', '00000002-0000-4000-8000-000000000007', '00000001-0000-4000-8000-000000000004', 'member'),
  ('00000009-0000-4000-8008-000000000001', '00000002-0000-4000-8000-000000000008', '0199f000-0000-7000-8000-000000000001', 'admin'),
  ('00000009-0000-4000-8008-000000000002', '00000002-0000-4000-8000-000000000008', '00000001-0000-4000-8000-000000000002', 'member');

-- The default states for every type, in the id scheme of 0002 (PP = 07 and 08).
INSERT INTO state (id, project_id, type, name, category, sort_order)
SELECT format('00000003-0000-4000-80%s-%s', lpad(p.number::text, 2, '0'), lpad((t.digit * 100 + s.sort_order)::text, 12, '0'))::uuid,
       project.id, t.type, s.name, s.category, s.sort_order
FROM (VALUES ('ARCH', 7), ('EMPT', 8)) AS p (key, number)
JOIN project ON project.key = p.key
CROSS JOIN (VALUES ('epic', 1), ('feature', 2), ('story', 3), ('bug', 4), ('task', 5)) AS t (type, digit)
CROSS JOIN (VALUES ('New', 'proposed', 1), ('Active', 'inProgress', 2), ('Resolved', 'resolved', 3), ('Closed', 'completed', 4), ('Removed', 'removed', 5)) AS s (name, category, sort_order);

-- Default boards for both projects, generated from their states exactly as 0004 does for the other projects.
INSERT INTO board (id, project_id, level, settings)
SELECT format('00000007-0000-4000-80%s-%s', lpad(p.number::text, 2, '0'), lpad(l.digit::text, 12, '0'))::uuid,
       project.id,
       l.level,
       jsonb_build_object(
           'columns', CASE WHEN l.level = 'taskboard' THEN '[]'::jsonb ELSE (
               SELECT jsonb_agg(jsonb_build_object(
                          'id', 'col-' || lower(replace(main.name, ' ', '-')),
                          'name', main.name,
                          'wipLimit', NULL::integer,
                          'split', false,
                          'definitionOfDone', '',
                          'stateMappings', CASE WHEN l.level = 'stories'
                              THEN jsonb_build_array(
                                       jsonb_build_object('type', 'story', 'stateId', main.id),
                                       jsonb_build_object('type', 'bug', 'stateId', bug.id))
                              ELSE jsonb_build_array(jsonb_build_object('type', l.main_type, 'stateId', main.id)) END)
                      ORDER BY main.sort_order)
               FROM state main
               LEFT JOIN state bug ON bug.project_id = main.project_id AND bug.type = 'bug' AND bug.name = main.name
               WHERE main.project_id = project.id AND main.type = l.main_type AND main.category <> 'removed') END,
           'swimlanes', CASE WHEN l.level = 'taskboard' THEN '[]'::jsonb ELSE
               jsonb_build_array(jsonb_build_object('id', 'lane-default', 'name', 'Default lane', 'colour', NULL::text, 'kind', 'default', 'criteria', NULL::jsonb)) END,
           'cardFields', ship.settings -> 'cardFields',
           'styleRules', '[]'::jsonb,
           'tagColours', '[]'::jsonb,
           'annotations', ship.settings -> 'annotations',
           'cardReordering', ship.settings -> 'cardReordering',
           'transitions', '[]'::jsonb)
FROM (VALUES ('ARCH', 7), ('EMPT', 8)) AS p (key, number)
JOIN project ON project.key = p.key
CROSS JOIN (VALUES ('stories', 1, 'story'), ('features', 2, 'feature'), ('epics', 3, 'epic'), ('taskboard', 4, 'task')) AS l (level, digit, main_type)
JOIN board ship ON ship.level = l.level AND ship.project_id = (SELECT id FROM project WHERE key = 'SHIP');

-- ARCH's work items: one Feature and four Stories, no sprints. Ids 00000005-0000-4000-8007-<number>.
-- anchor = the latest working day on or before today; items are created 63 days (nine weeks) before it, while Lee was still active (the same weekday, so a working day,
-- and never in the future). The Closed story reached Closed six hours later that day.
INSERT INTO work_item (id, project_id, number, key, type, title, state_id, assigned_to, sprint_id, priority, story_points,
                       backlog_order, parent_id, parent_type, tags, board_column_id, board_column_done, column_order,
                       revision, created_by, created_at, changed_by, changed_at, state_changed_at)
SELECT format('00000005-0000-4000-8007-%s', lpad(i.number::text, 12, '0'))::uuid,
       project.id,
       i.number,
       'ARCH-' || i.number,
       i.type,
       i.title,
       state.id,
       person.id,
       NULL,
       i.priority,
       i.points,
       lpad(i.backlog_rank::text, 8, '0'),
       parent.id,
       parent.type,
       i.tags,
       i.board_column_id,
       false,
       lpad(i.column_rank::text, 8, '0'),
       1,
       creator.id,
       created.at,
       creator.id,
       coalesce(closed.at, created.at),
       coalesce(closed.at, created.at)
FROM (VALUES
  (1, 'feature', 'Retire the invoice engine', NULL::int, 'Active', 13, 'mei', 2, ARRAY[]::text[], 'col-active', 1000, 1000),
  (2, 'story', 'Export open invoices to the new ledger', 1, 'Active', 5, 'mei', 2, ARRAY['Migration']::text[], 'col-active', 2000, 2000),
  (3, 'story', 'Freeze new invoice creation', 1, 'New', 3, NULL, 2, ARRAY[]::text[], 'col-new', 3000, 1000),
  (4, 'story', 'Archive the nightly statement job', 1, 'New', 2, NULL, 3, ARRAY['Tech debt']::text[], 'col-new', 4000, 2000),
  (5, 'story', 'Document the credit-note rules', 1, 'Closed', 2, 'mei', 3, ARRAY[]::text[], 'col-closed', 5000, 1000)
) AS i (number, type, title, parent_number, state_name, points, assignee, priority, tags, board_column_id, backlog_rank, column_rank)
JOIN project ON project.key = 'ARCH'
JOIN state ON state.project_id = project.id AND state.type = i.type AND state.name = i.state_name
LEFT JOIN app_user person ON lower(split_part(person.name, ' ', 1)) = i.assignee
LEFT JOIN (VALUES (1, 'feature'), (2, 'story'), (3, 'story'), (4, 'story'), (5, 'story')) AS parent_src (number, type) ON parent_src.number = i.parent_number
CROSS JOIN LATERAL (SELECT CASE WHEN parent_src.number IS NOT NULL THEN format('00000005-0000-4000-8007-%s', lpad(parent_src.number::text, 12, '0'))::uuid END AS id, parent_src.type AS type) AS parent
JOIN app_user creator ON creator.id = '00000001-0000-4000-8000-000000000009'
CROSS JOIN LATERAL (
    SELECT t.today - CASE extract(isodow FROM t.today)::int WHEN 6 THEN 1 WHEN 7 THEN 2 ELSE 0 END - 63 AS day
    FROM (SELECT (now() AT TIME ZONE 'utc')::date AS today) AS t
) AS base
CROSS JOIN LATERAL (SELECT ((base.day::timestamp + interval '9 hours') + i.number * interval '1 minute') AT TIME ZONE 'utc' AS at) AS created
LEFT JOIN LATERAL (SELECT ((base.day::timestamp + interval '15 hours') AT TIME ZONE 'utc') AS at WHERE i.state_name = 'Closed') AS closed ON true;

-- Revision 1 for every ARCH item, written from its row (see 0005 for the shape).
INSERT INTO revision (work_item_id, rev, changed_by, changed_at, changes)
SELECT w.id, 1, w.created_by, w.changed_at,
       (SELECT jsonb_agg(jsonb_build_object('field', f.field, 'from', f.from_value, 'to', f.to_value,
                                            'fromLabel', NULL::text, 'toLabel', f.to_label) ORDER BY f.ord)
        FROM (VALUES
            (1, 'title', NULL::jsonb, to_jsonb(w.title), NULL::text, true),
            (2, 'type', NULL, to_jsonb(w.type), NULL, true),
            (3, 'state', NULL, to_jsonb(w.state_id), state.name, true),
            (4, 'assignee', NULL, to_jsonb(w.assigned_to), person.name, w.assigned_to IS NOT NULL),
            (5, 'priority', NULL, to_jsonb(w.priority), NULL, true),
            (6, 'points', NULL, to_jsonb(trim_scale(w.story_points)), NULL, w.story_points IS NOT NULL),
            (7, 'tags', '[]'::jsonb, to_jsonb(w.tags), NULL, cardinality(w.tags) > 0),
            (8, 'sprint', NULL, to_jsonb(w.sprint_id), sprint.name, w.sprint_id IS NOT NULL),
            (9, 'parent', NULL, to_jsonb(w.parent_id), parent.title, w.parent_id IS NOT NULL)
        ) AS f (ord, field, from_value, to_value, to_label, included)
        WHERE f.included)
FROM work_item w
JOIN state ON state.id = w.state_id
LEFT JOIN app_user person ON person.id = w.assigned_to
LEFT JOIN sprint ON sprint.id = w.sprint_id
LEFT JOIN work_item parent ON parent.id = w.parent_id
WHERE w.project_id = (SELECT id FROM project WHERE key = 'ARCH');

-- Priya has a profile photo (refs.json pins her photoUrl): a blob ref and version 1. The bytes aren't in any blob store; tests
-- that need to serve a photo upload their own.
UPDATE app_user SET avatar_photo_blob = 'avatars/00000001000040008000000000000002', photo_version = 1
WHERE id = '00000001-0000-4000-8000-000000000002';
