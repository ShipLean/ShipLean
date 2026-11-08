Thanks for the report. Nothing in ShipLean does this yet, so here is a proposal.

**Story: Filter the backlog by tag**

Add a Tag field to the backlog filter, with `=`, `<>` and `in`.

Reply **LGTM** to take it into the backlog, or tell me what to change.

```shiplean-proposal
{
  "verdict": "new",
  "type": "story",
  "title": "Filter the backlog by tag",
  "description": "Add a **Tag** field to the backlog filter.\n\nIt supports `=`, `<>` and `in`, like the other list fields.",
  "acceptanceCriteria": [
    "The backlog filter offers a Tag field",
    "Tag = x shows only items tagged x"
  ],
  "parentKey": "SHIP-85",
  "existing": [],
  "requires": [],
  "questions": [],
  "confidence": "high"
}
```
