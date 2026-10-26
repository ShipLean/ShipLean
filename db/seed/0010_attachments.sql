-- Two text attachments by Joel on SHIP-90, so routes keyed on an attachment id have something to reach (the access
-- sweep's allowed leg pins an in-memory blob store holding their bytes under these storage refs). The second exists to
-- be archived by a test; neither is imported.
INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, uploaded_by, created_at)
VALUES
    ('00000005-0000-4000-8003-000000000901', '00000005-0000-4000-8001-000000000090', 'wallet-scope.txt', 'text/plain', 24,
     'seed/attachments/wallet-scope.txt', '0199f000-0000-7000-8000-000000000001', now() - interval '3 days'),
    ('00000005-0000-4000-8003-000000000902', '00000005-0000-4000-8001-000000000090', 'old-notes.txt', 'text/plain', 21,
     'seed/attachments/old-notes.txt', '0199f000-0000-7000-8000-000000000001', now() - interval '2 days');
