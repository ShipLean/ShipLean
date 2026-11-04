-- The BAU work item type (type id 'bau'): business-as-usual work that ShipLean makes itself, never by hand. It stands alone: no parent
-- and no sprint (ck_work_item_bau_alone), and ck_work_item_parent_type_allowed is left as it is, so no clause lets a bau item have a
-- parent or a task sit under one. The constraints that list the types are dropped and added again with 'bau' in them, and every project
-- that already has states gets the four BAU states. Migrations run before the seed, so a fresh database has no projects here; the seed
-- writes the BAU states of its own projects.
ALTER TABLE state DROP CONSTRAINT state_type_check;
ALTER TABLE state ADD CONSTRAINT state_type_check CHECK (type IN ('epic', 'feature', 'story', 'bug', 'task', 'action', 'bau'));

ALTER TABLE work_item DROP CONSTRAINT work_item_type_check;
ALTER TABLE work_item ADD CONSTRAINT work_item_type_check CHECK (type IN ('epic', 'feature', 'story', 'bug', 'task', 'action', 'bau'));

ALTER TABLE work_item ADD CONSTRAINT ck_work_item_bau_alone CHECK (type <> 'bau' OR (sprint_id IS NULL AND parent_id IS NULL));

INSERT INTO state (id, project_id, type, name, category, sort_order)
SELECT gen_random_uuid(), p.id, 'bau', s.name, s.category, s.sort_order
FROM project p
CROSS JOIN (VALUES ('New', 'proposed', 1), ('Active', 'inProgress', 2), ('Closed', 'completed', 3), ('Removed', 'removed', 4)) AS s (name, category, sort_order)
WHERE EXISTS (SELECT 1 FROM state st WHERE st.project_id = p.id AND st.type = 'story')
  AND NOT EXISTS (SELECT 1 FROM state a WHERE a.project_id = p.id AND a.type = 'bau');
