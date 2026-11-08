This already shipped. The Epic criteria field follows the whole parent chain and supports `=`, `<>`, `in` and `is empty`.

- PR #47, merge commit 54dc0d4
- Published 3 Nov 2026 22:19 UTC

To check your instance, open `/health`: it shows `version: 1.0.0+<sha>`. Your build has the fix when
`git merge-base --is-ancestor 54dc0d4 <sha>` succeeds.

```shiplean-proposal
{
  "verdict": "alreadyShipped",
  "type": "story",
  "title": "Filter the backlog by Epic",
  "existing": [
    {
      "key": "SHIP-412",
      "prUrl": "https://github.com/ShipLean/ShipLean/pull/47",
      "commit": "54dc0d4",
      "mergedAt": "2026-11-03T21:58:00Z",
      "publishedAt": "2026-11-03T22:19:00Z",
      "runUrl": "https://github.com/ShipLean/ShipLean/actions/runs/123456789"
    }
  ],
  "requires": ["Search indexing turned on"]
}
```
