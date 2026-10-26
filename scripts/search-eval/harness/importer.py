"""Corpus -> ShipLean over the existing REST endpoints. Items are created in file order so their keys are <KEY>-n."""
import json
from pathlib import Path

TYPES = {"epic": "epic", "feature": "feature", "story": "story", "bug": "bug", "task": "task"}


def corpus_files(corpus_dir):
    return sorted(Path(corpus_dir).glob("*.json"))


def import_corpus(api, corpus, log=print):
    """Imports one corpus dict. Returns {ref: key} for its items."""
    proj = corpus["project"]
    created = api.send("POST", "/api/projects", json={
        "name": proj["name"], "key": proj["key"], "copyFromProjectId": None,
        "memberEmails": [], "workingDays": [1, 2, 3, 4, 5], "firstSprint": None})
    pid = created["id"]
    _set_description(api, pid, proj["description"])
    removed_by_type, state_ids = None, {}
    for s in api.send("GET", f"/api/projects/{pid}/states"):
        state_ids[(s["type"], s["name"])] = s["id"]
        if s["category"] == "removed":
            removed_by_type = removed_by_type or {}
            removed_by_type[s["type"]] = s["id"]

    sprint_ids = {}
    for sp in corpus["sprints"]:
        made = api.send("POST", f"/api/projects/{pid}/sprints", json={
            "name": sp["name"], "startDate": sp["startDate"], "endDate": sp["endDate"], "goal": sp["goal"]})
        sprint_ids[sp["ref"]] = made["id"]

    ids, keys = {}, {}
    for n, item in enumerate(corpus["items"], start=1):
        changes = {
            "description": {"value": item["description"]},
            "acceptanceCriteria": {"value": [{"text": t, "done": False} for t in item["acceptanceCriteria"]]},
            "reproSteps": {"value": item["reproSteps"]},
            "tags": {"value": item["tags"]},
        }
        body = {
            "projectId": pid, "type": TYPES[item["type"]], "title": item["title"],
            "parentId": ids[item["parentRef"]] if item.get("parentRef") else None,
            "sprintId": sprint_ids[item["sprintRef"]] if item.get("sprintRef") else None,
            "changes": changes}
        made = api.send("POST", "/api/work-items", json=body)
        expected = f"{proj['key']}-{n}"
        if made["key"] != expected:
            raise RuntimeError(f"{item['ref']}: created as {made['key']}, expected {expected}")
        ids[item["ref"]] = made["id"]
        keys[item["ref"]] = made["key"]
        for c in item["comments"]:
            ids[c["ref"]] = api.send("POST", f"/api/work-items/{made['id']}/comments", json={"body": c["body"]})["id"]

    # States after every item exists, so a Closed or Removed parent never blocks its children being created.
    for item in corpus["items"]:
        if item["state"] == "New":
            continue
        if item["state"] == "Removed":
            state_id = removed_by_type[item["type"]]
        else:
            state_id = state_ids[(item["type"], item["state"])]
        current = api.send("GET", f"/api/work-items/{ids[item['ref']]}")
        api.send("PATCH", f"/api/work-items/{ids[item['ref']]}", json={
            "expectedRevision": current["revision"], "changes": {"state": {"value": state_id}}})

    for item in corpus["items"]:
        for c in item["comments"]:
            if c["archived"]:
                api.send("POST", f"/api/comments/{ids[c['ref']]}/archive")
    log(f"{proj['key']}: {len(keys)} items")
    return keys


def _set_description(api, pid, description):
    settings = api.send("GET", f"/api/projects/{pid}/settings")
    general = settings["general"]
    api.send("PUT", f"/api/projects/{pid}/settings", json={
        "general": {"key": general["key"], "name": general["name"], "description": description,
                    "workingDays": general["workingDays"]},
        "members": [{"email": m["email"], "role": m["role"]} for m in settings["members"]],
        "sprints": settings["sprints"],
        "states": {"add": [], "update": [], "reorder": [], "remove": []}})


def import_all(api, corpus_files_, results_dir, log=print):
    refmap = {}
    for path in corpus_files_:
        with open(path, encoding="utf-8") as f:
            refmap.update(import_corpus(api, json.load(f), log))
    out = Path(results_dir)
    out.mkdir(parents=True, exist_ok=True)
    (out / "refmap.json").write_text(json.dumps(refmap, indent=1), encoding="utf-8")
    return refmap
