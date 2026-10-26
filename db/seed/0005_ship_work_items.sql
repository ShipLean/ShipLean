-- SHIP's work items as drawn in design/screens/shared.js (stories, bugs, features, epics, tasks), with parents, assignees,
-- points, tags, backlog order and board position (board.html's layout where shared.js has none). SHIP-90..SHIP-183.
-- Not seeded: board.html's page-local extras (SHIP-146, 148, 149, 152, 157).
-- Bugs SHIP-153 and SHIP-160 sit in the Ready column, which maps Bug to Triaged, so they are Triaged.
-- Ids: 00000005-0000-4000-8001-<number>. Every item gets exactly one revision (rev 1, "created"), written from the rows.
-- Ranks (backlog_order, column_order) are zero-padded numbers that sort under COLLATE "C". Created times are 14 days
-- (28 for epics and features) before Sprint 19 starts, always on a working day. A Closed item's revision 1, changed_at and
-- state_changed_at are the time it closed (see the closed lateral below); a later history script rewrites rev 1.

INSERT INTO work_item (id, project_id, number, key, type, title, state_id, assigned_to, sprint_id, priority, story_points,
                       backlog_order, parent_id, parent_type, tags, board_column_id, board_column_done, column_order,
                       revision, created_by, created_at, changed_by, changed_at, state_changed_at)
SELECT format('00000005-0000-4000-8001-%s', lpad(i.number::text, 12, '0'))::uuid,
       project.id,
       i.number,
       'SHIP-' || i.number,
       i.type,
       i.title,
       state.id,
       person.id,
       sprint.id,
       i.priority,
       i.points,
       lpad(i.backlog_rank::text, 8, '0'),
       parent.id,
       parent.type,
       i.tags,
       i.board_column_id,
       i.board_column_done,
       lpad(i.column_rank::text, 8, '0'),
       1,
       creator.id,
       created.at,
       creator.id,
       coalesce(closed.at, created.at),
       coalesce(closed.at, created.at)
FROM (VALUES
  (141, 'story', 'Pay with saved card', 101, 'Active', 5, 24, 'priya', 2, ARRAY['Payments']::text[], 'col-in-progress', false, 1000, 12000),
  (138, 'bug', 'Totals wrong when coupon removed', 101, 'Closed', 2, 24, 'tom', 2, ARRAY['Payments']::text[], 'col-done', false, 2000, 16000),
  (144, 'story', 'Address autocomplete', 101, 'Active', 3, 24, 'mei', 2, '{}'::text[], 'col-in-progress', false, 3000, 14000),
  (129, 'story', 'Sign in with passkey', 102, 'Active', 8, 24, 'sam', 2, ARRAY['Mobile','Blocked']::text[], 'col-in-progress', false, 4000, 13000),
  (150, 'story', 'Order confirmation email', 101, 'Closed', 3, 24, 'ana', 2, '{}'::text[], 'col-done', false, 5000, 15000),
  (151, 'story', 'Remember device for 30 days', 102, 'New', 5, 25, NULL, 2, ARRAY['Mobile']::text[], 'col-ready', true, 6000, 9000),
  (153, 'bug', 'Crash when biometric prompt is cancelled', 102, 'Triaged', 3, 25, NULL, 2, ARRAY['Mobile']::text[], 'col-ready', true, 7000, 10000),
  (147, 'story', 'Export sprint report to CSV', 103, 'New', 5, 25, NULL, 2, '{}'::text[], 'col-ready', false, 8000, 7000),
  (156, 'story', 'Dashboard: cycle time widget', 108, 'New', 8, NULL, NULL, 3, '{}'::text[], 'col-new', false, 9000, 6000),
  (158, 'story', 'Gift card balance check', 101, 'New', 3, NULL, NULL, 2, ARRAY['Payments','Feedback Ticket']::text[], 'col-ready', true, 10000, 11000),
  (160, 'bug', 'Session expires during checkout', 101, 'Triaged', 2, NULL, NULL, 1, ARRAY['Feedback Ticket']::text[], 'col-ready', true, 11000, 1000),
  (162, 'story', 'Rotate API keys from settings', 104, 'New', 5, NULL, NULL, 3, ARRAY['Tech debt']::text[], 'col-ready', false, 12000, 8000),
  (163, 'story', 'Audit log retention setting', 104, 'New', 3, NULL, NULL, 3, ARRAY['Tech debt']::text[], 'col-new', false, 13000, 5000),
  (165, 'story', 'Weekly digest email', 103, 'New', 8, NULL, NULL, 3, '{}'::text[], 'col-new', false, 14000, 3000),
  (166, 'story', 'Saved filters on dashboard', 108, 'New', 5, NULL, NULL, 3, '{}'::text[], 'col-new', false, 15000, 4000),
  (170, 'story', 'SSO via Okta', 102, 'New', 13, NULL, NULL, 2, ARRAY['Security']::text[], 'col-new', false, 16000, 2000),
  (120, 'story', 'Guest checkout', 101, 'Closed', 5, 22, 'priya', 2, '{}'::text[], 'col-done', false, 17000, 20000),
  (124, 'story', 'Apple Pay button', 101, 'Closed', 3, 23, 'tom', 2, ARRAY['Payments']::text[], 'col-done', false, 18000, 19000),
  (118, 'story', 'Password reset by email', 102, 'Closed', 3, 22, 'sam', 2, ARRAY['Mobile']::text[], 'col-done', false, 19000, 21000),
  (126, 'bug', 'Wrong currency on receipts', 103, 'Closed', 2, 23, 'ana', 2, '{}'::text[], 'col-done', false, 20000, 18000),
  (131, 'story', 'Rate limiting on login', 104, 'Closed', 3, 23, 'mei', 2, ARRAY['Security']::text[], 'col-done', false, 21000, 17000),
  (101, 'feature', 'Checkout v2', 90, 'Active', 20, NULL, NULL, 2, '{}'::text[], 'col-in-progress', false, 1000, 1000),
  (102, 'feature', 'Mobile sign-in', 91, 'Active', 21, NULL, NULL, 2, '{}'::text[], 'col-in-progress', false, 2000, 2000),
  (103, 'feature', 'Reporting', 92, 'Active', 13, NULL, NULL, 2, '{}'::text[], 'col-in-progress', false, 3000, 3000),
  (108, 'feature', 'Dashboard widgets', 103, 'New', 8, NULL, NULL, 2, '{}'::text[], 'col-new', false, 4000, 4000),
  (104, 'feature', 'Ops hygiene', 93, 'New', 5, NULL, NULL, 2, '{}'::text[], 'col-new', false, 5000, 5000),
  (90, 'epic', 'Checkout conversion', NULL, 'Active', 40, NULL, NULL, 2, '{}'::text[], 'col-in-progress', false, 1000, 1000),
  (91, 'epic', 'Account security', NULL, 'Active', 30, NULL, NULL, 2, '{}'::text[], 'col-in-progress', false, 2000, 2000),
  (92, 'epic', 'Insights for merchants', NULL, 'New', 30, NULL, NULL, 2, '{}'::text[], 'col-new', false, 3000, 3000),
  (93, 'epic', 'Platform health', NULL, 'New', 10, NULL, NULL, 2, '{}'::text[], 'col-new', false, 4000, 4000),
  (171, 'task', 'API: tokenised card lookup', 141, 'In review', 2, 24, 'priya', 2, '{}'::text[], NULL, false, 1000, NULL),
  (172, 'task', 'UI: saved card picker', 141, 'Active', 2, 24, 'mei', 1, '{}'::text[], NULL, false, 2000, NULL),
  (173, 'task', 'E2E test', 141, 'New', 1, 24, NULL, 2, '{}'::text[], NULL, false, 3000, NULL),
  (174, 'task', 'Fix and regression test', 138, 'Closed', 2, 24, 'tom', 2, '{}'::text[], NULL, false, 4000, NULL),
  (175, 'task', 'Provider spike', 144, 'Closed', 1, 24, 'mei', 2, ARRAY['Spike']::text[], NULL, false, 5000, NULL),
  (176, 'task', 'Wire into the address form', 144, 'New', 2, 24, NULL, 2, '{}'::text[], NULL, false, 6000, NULL),
  (177, 'task', 'WebAuthn registration', 129, 'Closed', 3, 24, 'sam', 2, '{}'::text[], NULL, false, 7000, NULL),
  (178, 'task', 'WebAuthn sign-in', 129, 'Active', 3, 24, 'sam', 2, '{}'::text[], NULL, false, 8000, NULL),
  (179, 'task', 'Fallback to password', 129, 'New', 2, 24, NULL, 2, ARRAY['Blocked']::text[], NULL, false, 9000, NULL),
  (180, 'task', 'Feature flag', 129, 'Closed', 1, 24, 'ana', 3, '{}'::text[], NULL, false, 10000, NULL),
  (181, 'task', 'Email template', 150, 'Closed', 1, 24, 'ana', 2, '{}'::text[], NULL, false, 11000, NULL),
  (182, 'task', 'Rotate staging TLS certificates', NULL, 'New', NULL, 24, NULL, 2, ARRAY['Tech debt']::text[], NULL, false, 12000, NULL),
  (183, 'task', 'Update the on-call runbook', NULL, 'Active', NULL, 24, 'tom', 3, '{}'::text[], NULL, false, 13000, NULL)
) AS i (number, type, title, parent_number, state_name, points, sprint_number, assignee, priority, tags, board_column_id,
        board_column_done, backlog_rank, column_rank)
JOIN project ON project.key = 'SHIP'
JOIN state ON state.project_id = project.id AND state.type = i.type AND state.name = i.state_name
LEFT JOIN app_user person ON lower(split_part(person.name, ' ', 1)) = i.assignee
LEFT JOIN sprint ON sprint.project_id = project.id AND sprint.name = 'Sprint ' || i.sprint_number
LEFT JOIN (VALUES (141, 'story'), (138, 'bug'), (144, 'story'), (129, 'story'), (150, 'story'), (151, 'story'), (153, 'bug'), (147, 'story'), (156, 'story'), (158, 'story'), (160, 'bug'), (162, 'story'), (163, 'story'), (165, 'story'), (166, 'story'), (170, 'story'), (120, 'story'), (124, 'story'), (118, 'story'), (126, 'bug'), (131, 'story'), (101, 'feature'), (102, 'feature'), (103, 'feature'), (108, 'feature'), (104, 'feature'), (90, 'epic'), (91, 'epic'), (92, 'epic'), (93, 'epic'), (171, 'task'), (172, 'task'), (173, 'task'), (174, 'task'), (175, 'task'), (176, 'task'), (177, 'task'), (178, 'task'), (179, 'task'), (180, 'task'), (181, 'task'), (182, 'task'), (183, 'task')) AS parent_src (number, type) ON parent_src.number = i.parent_number
CROSS JOIN LATERAL (SELECT CASE WHEN parent_src.number IS NOT NULL THEN format('00000005-0000-4000-8001-%s', lpad(parent_src.number::text, 12, '0'))::uuid END AS id, parent_src.type AS type) AS parent
JOIN app_user creator ON creator.id = '00000001-0000-4000-8000-000000000002'
JOIN sprint first_sprint ON first_sprint.project_id = project.id AND first_sprint.name = 'Sprint 19'
CROSS JOIN LATERAL (
    SELECT ((first_sprint.start_date::timestamp + interval '9 hours')
            - CASE WHEN i.type IN ('epic', 'feature') THEN interval '28 days' ELSE interval '14 days' END
            + i.number * interval '1 minute') AT TIME ZONE 'utc' AS at
) AS created
LEFT JOIN LATERAL (
    -- When the item reached Closed: 15:00 UTC on a past working day inside its sprint. Past sprints: the last working day on
    -- or before end_date - 1. The current sprint: its second working day, which is before the anchor (its start is the
    -- 4th working day back, so this is never in the future).
    SELECT ((CASE WHEN sprint.end_date < (now() AT TIME ZONE 'utc')::date
                 THEN sprint.end_date - 1 - CASE extract(isodow FROM sprint.end_date - 1)::int WHEN 6 THEN 1 WHEN 7 THEN 2 ELSE 0 END
                 ELSE sprint.start_date + CASE extract(isodow FROM sprint.start_date)::int WHEN 5 THEN 3 ELSE 1 END
            END)::timestamp + interval '15 hours') AT TIME ZONE 'utc' AS at
    WHERE i.state_name = 'Closed'
) AS closed ON true;

-- Revision 1 for every drawn item: its current field values as changes from null (tags from []), in field-ids order,
-- with snapshot labels for user, state, sprint and work item values.
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
WHERE w.project_id = (SELECT id FROM project WHERE key = 'SHIP');
