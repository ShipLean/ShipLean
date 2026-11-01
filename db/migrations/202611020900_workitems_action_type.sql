-- The Action item work item type (type id 'action'): a story-level item that takes a Feature parent or none and can have Task
-- children. The constraints that list the types are dropped and added again with 'action' in them (no data changes shape), and
-- every project that already has states gets the four Action states (the default Resolved is left out). Migrations run before
-- the seed, so a fresh database has no projects here; the seed writes the Action states of its own projects.
ALTER TABLE state DROP CONSTRAINT state_type_check;
ALTER TABLE state ADD CONSTRAINT state_type_check CHECK (type IN ('epic', 'feature', 'story', 'bug', 'task', 'action'));

ALTER TABLE work_item DROP CONSTRAINT work_item_type_check;
ALTER TABLE work_item ADD CONSTRAINT work_item_type_check CHECK (type IN ('epic', 'feature', 'story', 'bug', 'task', 'action'));

ALTER TABLE work_item DROP CONSTRAINT ck_work_item_parent_type_allowed;
ALTER TABLE work_item ADD CONSTRAINT ck_work_item_parent_type_allowed CHECK (
    parent_type IS NULL
    OR (type = 'feature' AND parent_type IN ('epic', 'feature'))
    OR (type IN ('story', 'bug', 'action') AND parent_type = 'feature')
    OR (type = 'task' AND parent_type IN ('story', 'bug', 'action')));

INSERT INTO state (id, project_id, type, name, category, sort_order)
SELECT gen_random_uuid(), p.id, 'action', s.name, s.category, s.sort_order
FROM project p
CROSS JOIN (VALUES ('New', 'proposed', 1), ('Active', 'inProgress', 2), ('Closed', 'completed', 3), ('Removed', 'removed', 4)) AS s (name, category, sort_order)
WHERE EXISTS (SELECT 1 FROM state st WHERE st.project_id = p.id AND st.type = 'story')
  AND NOT EXISTS (SELECT 1 FROM state a WHERE a.project_id = p.id AND a.type = 'action');
