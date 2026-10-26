-- Two comments on SHIP-90 by Joel, so routes keyed on a comment id have something to reach (the access sweep's allowed
-- leg, discussion screens). The second exists to be archived by a test; neither is imported.
INSERT INTO comment (id, work_item_id, author_id, body, created_at, updated_at)
VALUES
    ('00000005-0000-4000-8002-000000000901', '00000005-0000-4000-8001-000000000090', '0199f000-0000-7000-8000-000000000001',
     'Scope agreed with design: card wallet first, saved cards later.', now() - interval '3 days', now() - interval '3 days'),
    ('00000005-0000-4000-8002-000000000902', '00000005-0000-4000-8001-000000000090', '0199f000-0000-7000-8000-000000000001',
     'Reminder: check the copy with legal before release.', now() - interval '2 days', now() - interval '2 days');
