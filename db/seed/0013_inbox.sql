-- Tom Okafor's Inbox (SeedIds.Inbox): a stale and a fresh status line and two @mentions. Everything is Tom's, as SHIP-922's seeded asks are, so
-- tests acting as Joel, Mei, Priya or Kai never see it in their Inbox.
-- Ids: 0000000c-0000-4000-8001-<n> mentions; their comments use the comment form 00000005-0000-4000-8002-<item><n> (SHIP-124: 1241, 1242).

-- SHIP-104 ("Ops hygiene") is Tom's, with a status line verified at the start of today (UTC), so it is never stale in the Inbox.
-- Revision 1 is rewritten to carry the assignee, so the revision replay still gives the row (no revision is added: tests send
-- SHIP-104's revision 1).
UPDATE work_item
SET assigned_to = '00000001-0000-4000-8000-000000000003',
    status_line = 'Key rotation and audit retention are next; nothing blocked.',
    status_verified_at = (now() AT TIME ZONE 'utc')::date::timestamp AT TIME ZONE 'utc',
    status_verified_by = '00000001-0000-4000-8000-000000000003'
WHERE id = '00000005-0000-4000-8001-000000000104';

UPDATE revision
SET changes = jsonb_insert(changes, '{3}', jsonb_build_object('field', 'assignee', 'from', NULL::jsonb,
                                                              'to', to_jsonb('00000001-0000-4000-8000-000000000003'::uuid),
                                                              'fromLabel', NULL::text, 'toLabel', 'Tom Okafor'))
WHERE work_item_id = '00000005-0000-4000-8001-000000000104' AND rev = 1
  AND NOT changes @> '[{"field":"assignee"}]'::jsonb;

-- SHIP-103 ("Reporting") is Tom's stale Feature: its status line was last verified at the start of the day 5 days before today (UTC),
-- so it shows in his Inbox. Revision 1 is rewritten to carry the assignee, as for SHIP-104 (no revision is added).
UPDATE work_item
SET assigned_to = '00000001-0000-4000-8000-000000000003',
    status_line = 'CSV export is in review; the weekly digest waits on the dashboard widgets.',
    status_verified_at = ((now() AT TIME ZONE 'utc')::date - 5)::timestamp AT TIME ZONE 'utc',
    status_verified_by = '00000001-0000-4000-8000-000000000003'
WHERE id = '00000005-0000-4000-8001-000000000103';

UPDATE revision
SET changes = jsonb_insert(changes, '{3}', jsonb_build_object('field', 'assignee', 'from', NULL::jsonb,
                                                              'to', to_jsonb('00000001-0000-4000-8000-000000000003'::uuid),
                                                              'fromLabel', NULL::text, 'toLabel', 'Tom Okafor'))
WHERE work_item_id = '00000005-0000-4000-8001-000000000103' AND rev = 1
  AND NOT changes @> '[{"field":"assignee"}]'::jsonb;

-- Two comments by Priya Shah on SHIP-124 (Tom's Story with the seeded asks), each naming @Tom Okafor: one mention still open, one Tom
-- has marked seen.
INSERT INTO comment (id, work_item_id, author_id, body, created_at, updated_at)
VALUES
    ('00000005-0000-4000-8002-000000001241', '00000005-0000-4000-8001-000000000124', '00000001-0000-4000-8000-000000000002',
     '@Tom Okafor can you confirm the merchant ID before Friday? Finance needs it for the production build.',
     (((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc' + interval '9 hours'), (((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc' + interval '9 hours')),
    ('00000005-0000-4000-8002-000000001242', '00000005-0000-4000-8001-000000000124', '00000001-0000-4000-8000-000000000002',
     'Design for the Apple Pay error is in the shared folder, @Tom Okafor.',
     (((now() AT TIME ZONE 'utc')::date - 3)::timestamp AT TIME ZONE 'utc' + interval '9 hours'), (((now() AT TIME ZONE 'utc')::date - 3)::timestamp AT TIME ZONE 'utc' + interval '9 hours'));

INSERT INTO comment_mention (id, comment_id, work_item_id, user_id, state, created_at, closed_at)
VALUES
    ('0000000c-0000-4000-8001-000000000001', '00000005-0000-4000-8002-000000001241', '00000005-0000-4000-8001-000000000124',
     '00000001-0000-4000-8000-000000000003', 'open', (((now() AT TIME ZONE 'utc')::date - 1)::timestamp AT TIME ZONE 'utc' + interval '9 hours'), NULL),
    ('0000000c-0000-4000-8001-000000000002', '00000005-0000-4000-8002-000000001242', '00000005-0000-4000-8001-000000000124',
     '00000001-0000-4000-8000-000000000003', 'seen', (((now() AT TIME ZONE 'utc')::date - 3)::timestamp AT TIME ZONE 'utc' + interval '9 hours'), (((now() AT TIME ZONE 'utc')::date - 2)::timestamp AT TIME ZONE 'utc' + interval '9 hours'));
