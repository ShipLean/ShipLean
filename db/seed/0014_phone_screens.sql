-- The phone app's screens (SeedIds.Screens): Tom's at-risk and blocked Features, Joel's Sprint 24 retro with a point Priya supports,
-- and when Tom last saw SHIP's Feature digest.
-- Ids: 0000000d-0000-4000-8001-<n>: 1 the retro, 2 its point, 3 Priya's support.

-- SHIP-102 ("Mobile sign-in") is at risk and SHIP-108 ("Dashboard widgets") blocked, both Tom's, with health set by Tom at the start of
-- yesterday (UTC). SHIP-108's status line says what blocks it, verified at the same time. Revision 1 of each is rewritten to carry the
-- assignee, so the revision replay still gives the row (no revision is added: tests send revision 1).
UPDATE work_item
SET assigned_to = '00000001-0000-4000-8000-000000000003',
    health = CASE id WHEN '00000005-0000-4000-8001-000000000102' THEN 'atRisk' ELSE 'blocked' END,
    health_set_at = ((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc',
    health_set_by = '00000001-0000-4000-8000-000000000003'
WHERE id IN ('00000005-0000-4000-8001-000000000102', '00000005-0000-4000-8001-000000000108');

UPDATE work_item
SET status_line = 'Widget data access is blocked on the sign-in decision.',
    status_verified_at = ((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc',
    status_verified_by = '00000001-0000-4000-8000-000000000003'
WHERE id = '00000005-0000-4000-8001-000000000108';

UPDATE revision
SET changes = jsonb_insert(changes, '{3}', jsonb_build_object('field', 'assignee', 'from', NULL::jsonb,
                                                              'to', to_jsonb('00000001-0000-4000-8000-000000000003'::uuid),
                                                              'fromLabel', NULL::text, 'toLabel', 'Tom Okafor'))
WHERE work_item_id IN ('00000005-0000-4000-8001-000000000102', '00000005-0000-4000-8001-000000000108') AND rev = 1;

-- Joel's person retro on Sprint 24 (current), written yesterday at 10:00 UTC, with one improve point; Priya supports it at 12:00.
INSERT INTO retrospective (id, project_id, sprint_id, author_user_id, role, created_at, updated_at)
VALUES ('0000000d-0000-4000-8001-000000000001', '00000002-0000-4000-8000-000000000001', '00000004-0000-4000-8001-000000000024',
        '0199f000-0000-7000-8000-000000000001', 'person',
        ((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc' + interval '10 hours',
        ((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc' + interval '10 hours');

INSERT INTO retro_point (id, retrospective_id, category, text, created_by_user_id, position, created_at)
VALUES ('0000000d-0000-4000-8001-000000000002', '0000000d-0000-4000-8001-000000000001', 'improve',
        'Agree the sign-in decision before the sprint starts, so Mobile sign-in is not blocked mid-sprint.',
        '0199f000-0000-7000-8000-000000000001', 0,
        ((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc' + interval '10 hours');

INSERT INTO retro_point_support (id, point_id, user_id, created_at)
VALUES ('0000000d-0000-4000-8001-000000000003', '0000000d-0000-4000-8001-000000000002', '00000001-0000-4000-8000-000000000002',
        ((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc' + interval '12 hours');

-- Tom last saw SHIP's Feature digest at the start of the day before yesterday (UTC).
INSERT INTO feature_digest_seen (user_id, project_id, seen_at)
VALUES ('00000001-0000-4000-8000-000000000003', '00000002-0000-4000-8000-000000000001',
        ((now() AT TIME ZONE 'utc')::date - 2)::timestamp AT TIME ZONE 'utc');
