-- A webhook can also link an item because the "ShipLean IDs:" line that opens a pull or merge request's body names it or
-- its Feature.
ALTER TABLE work_item_link DROP CONSTRAINT work_item_link_matched_on_check;
ALTER TABLE work_item_link ADD CONSTRAINT work_item_link_matched_on_check CHECK (matched_on IN ('title', 'branch', 'body'));
