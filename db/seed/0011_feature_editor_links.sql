-- Two URL links on SHIP-101 (Checkout v2, a Feature) by Joel, so routes keyed on a link id have something to reach (the
-- access sweep's allowed leg). The first is a GitHub pull request with a stored preview: its image and favicon keys name
-- blobs that SeedAttachmentBlobs puts into a pinned blob store. The second is a page link and exists to be archived by a
-- test. Neither is imported.
INSERT INTO work_item_link (
    id, source_id, link_type, url, note, kind,
    preview_title, preview_description, preview_site, preview_image_key, preview_favicon_key, preview_status, preview_fetched_at,
    number, location, author, state, state_source, state_as_of, created_by, created_at)
VALUES
    ('00000005-0000-4000-8004-000000000101', '00000005-0000-4000-8001-000000000101', 'url',
     'https://github.com/acme/storefront/pull/482', 'Needs the saved-card picker from SHIP-142', 'githubPull',
     'Add the saved-card picker to checkout', 'Lets a returning customer pick a saved card at the payment step.', 'GitHub',
     'seed/links/preview.png', 'seed/links/favicon.svg', 'ok', now() - interval '1 hour',
     482, 'acme/storefront', 'priya', 'open', 'live', now() - interval '1 hour',
     '0199f000-0000-7000-8000-000000000001', now() - interval '2 days'),
    ('00000005-0000-4000-8004-000000000102', '00000005-0000-4000-8001-000000000101', 'url',
     'https://wiki.example.com/checkout/v2-rollout', NULL, 'page',
     NULL, NULL, NULL, NULL, NULL, 'none', NULL,
     NULL, NULL, NULL, NULL, 'none', NULL,
     '0199f000-0000-7000-8000-000000000001', now() - interval '1 day');
