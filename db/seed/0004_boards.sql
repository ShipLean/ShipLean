-- Board rows: four per project (stories, features, epics, taskboard). Ids: 00000007-0000-4000-80PP-<level 1..4>.
-- SHIP's settings are the pinned fixture files verbatim (src/ShipLean.TestSupport/PinnedShapes/board-settings.*.json).

INSERT INTO board (id, project_id, level, settings)
VALUES
  ('00000007-0000-4000-8001-000000000001', (SELECT id FROM project WHERE key = 'SHIP'), 'stories', '{
  "columns": [
    {
      "id": "col-new",
      "name": "New",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Title says who it is for and why\n- Linked to a parent **Feature**",
      "stateMappings": [
        { "type": "story", "stateId": "00000003-0000-4000-8001-000000000301" },
        { "type": "bug", "stateId": "00000003-0000-4000-8001-000000000401" }
      ]
    },
    {
      "id": "col-ready",
      "name": "Ready",
      "wipLimit": 8,
      "split": true,
      "definitionOfDone": "- Acceptance criteria written and agreed\n- Estimated in story points\n- No open questions for the product owner\n- Bugs: **repro steps** and severity set",
      "stateMappings": [
        { "type": "story", "stateId": "00000003-0000-4000-8001-000000000301" },
        { "type": "bug", "stateId": "00000003-0000-4000-8001-000000000402" }
      ]
    },
    {
      "id": "col-in-progress",
      "name": "In progress",
      "wipLimit": 5,
      "split": false,
      "definitionOfDone": "- Code and tests pushed on a branch\n- Tasks exist for the remaining work\n- Draft PR open and linked",
      "stateMappings": [
        { "type": "story", "stateId": "00000003-0000-4000-8001-000000000302" },
        { "type": "bug", "stateId": "00000003-0000-4000-8001-000000000403" }
      ]
    },
    {
      "id": "col-in-review",
      "name": "In review",
      "wipLimit": 2,
      "split": false,
      "definitionOfDone": "- PR approved by one reviewer\n- CI green\n- Deployed to staging behind its flag",
      "stateMappings": [
        { "type": "story", "stateId": "00000003-0000-4000-8001-000000000303" },
        { "type": "bug", "stateId": "00000003-0000-4000-8001-000000000404" }
      ]
    },
    {
      "id": "col-done",
      "name": "Done",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Acceptance criteria checked on staging\n- Released, or flag on in production\n- Changelog updated",
      "stateMappings": [
        { "type": "story", "stateId": "00000003-0000-4000-8001-000000000304" },
        { "type": "bug", "stateId": "00000003-0000-4000-8001-000000000405" }
      ]
    }
  ],
  "swimlanes": [
    {
      "id": "lane-expedite",
      "name": "Expedite",
      "colour": "warn",
      "kind": "criteria",
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "priority", "op": "=", "value": 1 }
        ]
      }
    },
    {
      "id": "lane-default",
      "name": "Default lane",
      "colour": null,
      "kind": "default",
      "criteria": null
    }
  ],
  "cardFields": [
    { "type": "story", "showKey": true, "showAssignee": true, "showTags": true, "showPoints": true, "showEmptyFields": false, "extraFields": ["parent", "priority", "sprint"] },
    { "type": "bug", "showKey": true, "showAssignee": true, "showTags": true, "showPoints": true, "showEmptyFields": false, "extraFields": ["parent", "severity", "priority"] }
  ],
  "styleRules": [
    {
      "id": "rule-blocked",
      "name": "Blocked",
      "enabled": true,
      "colour": "danger",
      "bold": false,
      "italic": false,
      "underline": false,
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "tags", "op": "contains", "value": "Blocked" }
        ]
      }
    },
    {
      "id": "rule-spike",
      "name": "Spike",
      "enabled": true,
      "colour": "purple",
      "bold": false,
      "italic": false,
      "underline": false,
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "tags", "op": "contains", "value": "Spike" }
        ]
      }
    }
  ],
  "tagColours": [
    { "tag": "Payments", "colour": "accent" },
    { "tag": "Mobile", "colour": "info" },
    { "tag": "Feedback Ticket", "colour": "warn" },
    { "tag": "Tech debt", "colour": "muted" },
    { "tag": "Security", "colour": "ok" },
    { "tag": "Blocked", "colour": "danger" },
    { "tag": "Spike", "colour": "purple" }
  ],
  "annotations": { "tasks": true, "stories": false, "bugs": false, "features": false },
  "cardReordering": "changesBacklogOrder",
  "transitions": []
}'::jsonb),
  ('00000007-0000-4000-8001-000000000002', (SELECT id FROM project WHERE key = 'SHIP'), 'features', '{
  "columns": [
    {
      "id": "col-new",
      "name": "New",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Problem and the outcome we want, in one paragraph\n- Linked to an **Epic**",
      "stateMappings": [
        { "type": "feature", "stateId": "00000003-0000-4000-8001-000000000201" }
      ]
    },
    {
      "id": "col-shaping",
      "name": "Shaping",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Stories split out and estimated\n- Fits in about three sprints\n- Open questions listed on the item",
      "stateMappings": [
        { "type": "feature", "stateId": "00000003-0000-4000-8001-000000000201" }
      ]
    },
    {
      "id": "col-in-progress",
      "name": "In progress",
      "wipLimit": 3,
      "split": false,
      "definitionOfDone": "- Stories are being pulled into sprints\n- Behind a feature flag",
      "stateMappings": [
        { "type": "feature", "stateId": "00000003-0000-4000-8001-000000000202" }
      ]
    },
    {
      "id": "col-releasing",
      "name": "Releasing",
      "wipLimit": 2,
      "split": false,
      "definitionOfDone": "- Every story and bug closed\n- Rolling out behind its flag",
      "stateMappings": [
        { "type": "feature", "stateId": "00000003-0000-4000-8001-000000000203" }
      ]
    },
    {
      "id": "col-done",
      "name": "Done",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Flag on for everyone\n- Outcome measured and noted on the item",
      "stateMappings": [
        { "type": "feature", "stateId": "00000003-0000-4000-8001-000000000204" }
      ]
    }
  ],
  "swimlanes": [
    {
      "id": "lane-default",
      "name": "Default lane",
      "colour": null,
      "kind": "default",
      "criteria": null
    }
  ],
  "cardFields": [
    { "type": "feature", "showKey": true, "showAssignee": true, "showTags": true, "showPoints": true, "showEmptyFields": false, "extraFields": ["parent"] }
  ],
  "styleRules": [
    {
      "id": "rule-blocked",
      "name": "Blocked",
      "enabled": true,
      "colour": "danger",
      "bold": false,
      "italic": false,
      "underline": false,
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "tags", "op": "contains", "value": "Blocked" }
        ]
      }
    }
  ],
  "tagColours": [
    { "tag": "Payments", "colour": "accent" },
    { "tag": "Mobile", "colour": "info" },
    { "tag": "Feedback Ticket", "colour": "warn" },
    { "tag": "Tech debt", "colour": "muted" },
    { "tag": "Security", "colour": "ok" },
    { "tag": "Blocked", "colour": "danger" }
  ],
  "annotations": { "tasks": false, "stories": true, "bugs": true, "features": true },
  "cardReordering": "changesBacklogOrder",
  "transitions": []
}'::jsonb),
  ('00000007-0000-4000-8001-000000000003', (SELECT id FROM project WHERE key = 'SHIP'), 'epics', '{
  "columns": [
    {
      "id": "col-new",
      "name": "New",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Why it matters, and how we will know it worked",
      "stateMappings": [
        { "type": "epic", "stateId": "00000003-0000-4000-8001-000000000101" }
      ]
    },
    {
      "id": "col-in-progress",
      "name": "In progress",
      "wipLimit": 3,
      "split": false,
      "definitionOfDone": "- Features split out and ranked\n- At least one Feature in progress",
      "stateMappings": [
        { "type": "epic", "stateId": "00000003-0000-4000-8001-000000000102" }
      ]
    },
    {
      "id": "col-rolling-out",
      "name": "Rolling out",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Every Feature resolved or closed",
      "stateMappings": [
        { "type": "epic", "stateId": "00000003-0000-4000-8001-000000000103" }
      ]
    },
    {
      "id": "col-done",
      "name": "Done",
      "wipLimit": null,
      "split": false,
      "definitionOfDone": "- Outcome reviewed with stakeholders",
      "stateMappings": [
        { "type": "epic", "stateId": "00000003-0000-4000-8001-000000000104" }
      ]
    }
  ],
  "swimlanes": [
    {
      "id": "lane-default",
      "name": "Default lane",
      "colour": null,
      "kind": "default",
      "criteria": null
    }
  ],
  "cardFields": [
    { "type": "epic", "showKey": true, "showAssignee": true, "showTags": true, "showPoints": true, "showEmptyFields": false, "extraFields": [] }
  ],
  "styleRules": [
    {
      "id": "rule-blocked",
      "name": "Blocked",
      "enabled": true,
      "colour": "danger",
      "bold": false,
      "italic": false,
      "underline": false,
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "tags", "op": "contains", "value": "Blocked" }
        ]
      }
    }
  ],
  "tagColours": [
    { "tag": "Payments", "colour": "accent" },
    { "tag": "Mobile", "colour": "info" },
    { "tag": "Feedback Ticket", "colour": "warn" },
    { "tag": "Tech debt", "colour": "muted" },
    { "tag": "Security", "colour": "ok" },
    { "tag": "Blocked", "colour": "danger" }
  ],
  "annotations": { "tasks": false, "stories": true, "bugs": true, "features": true },
  "cardReordering": "changesBacklogOrder",
  "transitions": []
}'::jsonb),
  ('00000007-0000-4000-8001-000000000004', (SELECT id FROM project WHERE key = 'SHIP'), 'taskboard', '{
  "columns": [],
  "swimlanes": [],
  "cardFields": [
    { "type": "task", "showKey": true, "showAssignee": true, "showTags": true, "showPoints": false, "showEmptyFields": false, "extraFields": ["parent"] },
    { "type": "story", "showKey": true, "showAssignee": true, "showTags": false, "showPoints": true, "showEmptyFields": false, "extraFields": ["state"] },
    { "type": "bug", "showKey": true, "showAssignee": true, "showTags": false, "showPoints": true, "showEmptyFields": false, "extraFields": ["state"] }
  ],
  "styleRules": [
    {
      "id": "rule-blocked",
      "name": "Blocked",
      "enabled": true,
      "colour": "danger",
      "bold": false,
      "italic": false,
      "underline": false,
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "tags", "op": "contains", "value": "Blocked" }
        ]
      }
    },
    {
      "id": "rule-priority-1",
      "name": "Priority 1",
      "enabled": true,
      "colour": "warn",
      "bold": true,
      "italic": false,
      "underline": false,
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "priority", "op": "=", "value": 1 }
        ]
      }
    },
    {
      "id": "rule-waiting-on-review",
      "name": "Waiting on review",
      "enabled": false,
      "colour": "info",
      "bold": false,
      "italic": false,
      "underline": false,
      "criteria": {
        "match": "all",
        "clauses": [
          { "field": "stateCategory", "op": "=", "value": "resolved" },
          { "field": "assignee", "op": "=", "value": "@me" }
        ]
      }
    }
  ],
  "tagColours": [
    { "tag": "Blocked", "colour": "danger" },
    { "tag": "Spike", "colour": "info" },
    { "tag": "Tech debt", "colour": "warn" }
  ],
  "annotations": { "tasks": true, "stories": false, "bugs": false, "features": false },
  "cardReordering": null,
  "transitions": []
}'::jsonb);

-- The other projects: one column per non-Removed state of the level's type (Stories: the story states, bugs mapped to the
-- bug state of the same name), one default lane, no style rules, no tag colours; card fields, annotations and card
-- reordering are SHIP's for the same level. The Taskboard keeps empty columns and swimlanes like SHIP's.
INSERT INTO board (id, project_id, level, settings)
SELECT format('00000007-0000-4000-80%s-%s', lpad(p.number::text, 2, '0'), lpad(l.digit::text, 12, '0'))::uuid,
       project.id,
       l.level,
       jsonb_build_object(
           'columns', CASE WHEN l.level = 'taskboard' THEN '[]'::jsonb ELSE (
               SELECT jsonb_agg(jsonb_build_object(
                          'id', 'col-' || lower(replace(main.name, ' ', '-')),
                          'name', main.name,
                          'wipLimit', NULL::integer,
                          'split', false,
                          'definitionOfDone', '',
                          'stateMappings', CASE WHEN l.level = 'stories'
                              THEN jsonb_build_array(
                                       jsonb_build_object('type', 'story', 'stateId', main.id),
                                       jsonb_build_object('type', 'bug', 'stateId', bug.id))
                              ELSE jsonb_build_array(jsonb_build_object('type', l.main_type, 'stateId', main.id)) END)
                      ORDER BY main.sort_order)
               FROM state main
               LEFT JOIN state bug ON bug.project_id = main.project_id AND bug.type = 'bug' AND bug.name = main.name
               WHERE main.project_id = project.id AND main.type = l.main_type AND main.category <> 'removed') END,
           'swimlanes', CASE WHEN l.level = 'taskboard' THEN '[]'::jsonb ELSE
               jsonb_build_array(jsonb_build_object('id', 'lane-default', 'name', 'Default lane', 'colour', NULL::text, 'kind', 'default', 'criteria', NULL::jsonb)) END,
           'cardFields', ship.settings -> 'cardFields',
           'styleRules', '[]'::jsonb,
           'tagColours', '[]'::jsonb,
           'annotations', ship.settings -> 'annotations',
           'cardReordering', ship.settings -> 'cardReordering',
           'transitions', '[]'::jsonb)
FROM (VALUES ('PAY', 2), ('MOB', 3), ('DATA', 4), ('OPS', 5), ('PART', 6)) AS p (key, number)
JOIN project ON project.key = p.key
CROSS JOIN (VALUES ('stories', 1, 'story'), ('features', 2, 'feature'), ('epics', 3, 'epic'), ('taskboard', 4, 'task')) AS l (level, digit, main_type)
JOIN board ship ON ship.level = l.level AND ship.project_id = (SELECT id FROM project WHERE key = 'SHIP');
