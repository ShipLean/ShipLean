-- D7 (SHIP-744): Issue intake's Proposal instructions move into Prompts as the Issue proposal prompt. A project's saved text becomes
-- its custom v1 (source 'migrated'); the old project_issue_intake.proposal_instructions column stays (additive migrations) and is
-- no longer read or written. Idempotent: the built-in row is ON CONFLICT DO NOTHING and a project with an issueProposal version is skipped.
-- The built-in text below equalled IssueIntakeSettingsService.DefaultInstructions when this was written (D18); a released migration can't change.
INSERT INTO prompt_built_in (prompt, hash, text, first_seen_at)
VALUES ('issueProposal', 'b02d7c17af0210aed0a4abb6617998042ca6ad4f3d4fe3380fe27a2ceeda8ed0', $$Write a proposal for this issue to the project's standards.$$, now())
ON CONFLICT DO NOTHING;

-- Texts over 65,536 bytes are skipped (the prompt_version limit); none exists today (the old cap was 20,000 characters).
-- Texts with agent-tag errors migrate as they are: the resolver serves them untagged-only (D20) and the Prompts tab flags them.
WITH source AS (
    SELECT i.project_id, replace(btrim(i.proposal_instructions, E' \t\r\n'), E'\r\n', E'\n') AS text, i.updated_by, i.updated_at
    FROM project_issue_intake i
    WHERE btrim(i.proposal_instructions, E' \t\r\n') <> ''
      AND octet_length(replace(btrim(i.proposal_instructions, E' \t\r\n'), E'\r\n', E'\n')) <= 65536
      AND NOT EXISTS (SELECT 1 FROM prompt_version v WHERE v.project_id = i.project_id AND v.prompt = 'issueProposal')
), inserted AS (
    INSERT INTO prompt_version (id, project_id, prompt, number, text, base_hash, note, source, created_by, created_at)
    SELECT gen_random_uuid(), s.project_id, 'issueProposal', 1, s.text, 'b02d7c17af0210aed0a4abb6617998042ca6ad4f3d4fe3380fe27a2ceeda8ed0', 'Moved from Integrations › Proposal instructions', 'migrated', s.updated_by, s.updated_at
    FROM source s
    RETURNING id, project_id, created_by, created_at
)
INSERT INTO project_prompt (project_id, prompt, in_use_version_id, kept_built_in_hash, updated_by, updated_at)
SELECT n.project_id, 'issueProposal', n.id, NULL, n.created_by, n.created_at
FROM inserted n
ON CONFLICT (project_id, prompt) DO UPDATE SET in_use_version_id = EXCLUDED.in_use_version_id, updated_by = EXCLUDED.updated_by, updated_at = EXCLUDED.updated_at;
