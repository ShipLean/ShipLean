-- Open decisions and questions on SHIP-124 ("Apple Pay button"), Tom's Story (SeedIds.Asks), for the access sweep's asks rows:
-- D1 confirm, D2 change, D3 withdraw, D4 discuss, Q1 answer, Q2 withdraw. Its owner (coalesce(assigned_to, created_by)) is Tom, so
-- tests acting as Joel, Mei, Priya or Kai never wait on them. Raised by Tom through his agent two days ago, each with its event.
-- Ids: 0000000b-0000-4000-8001-<n> decisions, -8002-<n> questions, -8003-<n> their raised and asked events.
INSERT INTO decision (id, work_item_id, number, question, recommended, alternatives, current_answer, created_by_user_id,
                      created_agent_name, created_at, updated_at)
SELECT d.id::uuid, '00000005-0000-4000-8001-000000000124', d.number, d.question, d.recommended, d.alternatives::jsonb, d.recommended,
       '00000001-0000-4000-8000-000000000003', 'Claude Code', now() - interval '2 days', now() - interval '2 days'
FROM (VALUES
    ('0000000b-0000-4000-8001-000000000001', 1, 'Where does the Apple Pay button sit on the payment step?',
     'Above the card form', '["Below the card form", "In the order summary"]'),
    ('0000000b-0000-4000-8001-000000000002', 2, 'Which browsers show the button?',
     'Only browsers where Apple Pay is set up', '["Every Safari browser", "Every browser, disabled where unavailable"]'),
    ('0000000b-0000-4000-8001-000000000003', 3, 'Which button style do we use?',
     'Black, the system default', '["White with an outline"]'),
    ('0000000b-0000-4000-8001-000000000004', 4, 'Do we keep the card form when Apple Pay is offered?',
     'Yes, collapsed under "Pay by card"', '["No, hide it"]')
) AS d (id, number, question, recommended, alternatives);

INSERT INTO question (id, work_item_id, number, text, created_by_user_id, created_agent_name, created_at)
SELECT q.id::uuid, '00000005-0000-4000-8001-000000000124', q.number, q.text, '00000001-0000-4000-8000-000000000003', 'Claude Code',
       now() - interval '2 days'
FROM (VALUES
    ('0000000b-0000-4000-8002-000000000001', 1, 'Which merchant ID does the production build use?'),
    ('0000000b-0000-4000-8002-000000000002', 2, 'Is there a design for the error shown when Apple Pay fails?')
) AS q (id, number, text);

INSERT INTO ask_event (id, work_item_id, decision_id, question_id, kind, to_answer, to_kind, by_user_id, agent_name, at)
SELECT ('0000000b-0000-4000-8003-00000000000' || d.number)::uuid, d.work_item_id, d.id, NULL, 'raised', d.recommended, 'recommended',
       d.created_by_user_id, d.created_agent_name, d.created_at
FROM decision d WHERE d.work_item_id = '00000005-0000-4000-8001-000000000124'
UNION ALL
SELECT ('0000000b-0000-4000-8003-00000000001' || q.number)::uuid, q.work_item_id, NULL, q.id, 'asked', NULL, NULL,
       q.created_by_user_id, q.created_agent_name, q.created_at
FROM question q WHERE q.work_item_id = '00000005-0000-4000-8001-000000000124';
