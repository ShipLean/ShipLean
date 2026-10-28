-- True on a revision written by a bulk path (a settings save remapping items out of a removed state, a sprint rollover, an MCP
-- backlog move): the person who saved did not move each item, so it never starts an agent session.
ALTER TABLE revision ADD COLUMN bulk boolean NOT NULL DEFAULT false;
