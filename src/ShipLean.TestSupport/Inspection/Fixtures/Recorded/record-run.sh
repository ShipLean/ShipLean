#!/usr/bin/env bash
# Records a real Claude Code run as the claude-code-recorded fixture pair (K7). Not run by any test.
#   scripts/inspection-stack.sh up                      first (collector on 14318, ClickHouse on 18123)
#   record-run.sh run <scratch-dir>                     run a tiny session; prints the session id
#   record-run.sh export <session-id> <out-dir>         read it back from ClickHouse, redact, write
#                                                       claude-code-recorded.otlp.json and .expected.json
# The session uses a stand-in MCP server named "shiplean" (a 10-line stdio script, written into the scratch dir) so no real ShipLean is
# touched; the prompt asks for pick_up SHIP-1573 and then a failing `ls`. Content logging is left at the default (off).
set -euo pipefail
ch_url="${CH_URL:-http://127.0.0.1:18123/?database=otel}"
ch_auth="${CH_AUTH:-shiplean:shiplean}"

run() {
  local dir="${1:?scratch dir}"
  mkdir -p "$dir/work"
  cat > "$dir/fake_shiplean.py" <<'PY'
import sys, json
for line in sys.stdin:
    m = json.loads(line); i = m.get("id"); meth = m.get("method")
    if meth == "initialize":
        r = {"protocolVersion": m["params"]["protocolVersion"], "capabilities": {"tools": {}}, "serverInfo": {"name": "shiplean", "version": "0"}}
    elif meth == "tools/list":
        r = {"tools": [{"name": "pick_up", "description": "Pick up a work item", "inputSchema": {"type": "object", "properties": {"item": {"type": "string"}}, "required": ["item"]}}]}
    elif meth == "tools/call":
        r = {"content": [{"type": "text", "text": "picked up " + m["params"]["arguments"].get("item", "")}]}
    else:
        continue
    print(json.dumps({"jsonrpc": "2.0", "id": i, "result": r}), flush=True)
PY
  printf '{"mcpServers":{"shiplean":{"command":"python3","args":["%s/fake_shiplean.py"]}}}\n' "$dir" > "$dir/mcp.json"
  local tag="rec-$(date +%s)"
  (cd "$dir/work" && env CLAUDE_CODE_ENABLE_TELEMETRY=1 CLAUDE_CODE_ENHANCED_TELEMETRY_BETA=1 \
    OTEL_TRACES_EXPORTER=otlp OTEL_LOGS_EXPORTER=otlp OTEL_METRICS_EXPORTER=none \
    OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf OTEL_EXPORTER_OTLP_ENDPOINT="${RECORD_OTLP_ENDPOINT:-http://127.0.0.1:14318}" \
    OTEL_TRACES_EXPORT_INTERVAL=1000 OTEL_LOGS_EXPORT_INTERVAL=1000 \
    OTEL_RESOURCE_ATTRIBUTES="shiplean.project=SHIP,shiplean.item=SHIP-1573,vcs.repository.name=ShipLean,vcs.ref.head.name=t1463/SHIP-1573,run.tag=$tag" \
    timeout 180 "${CLAUDE:-$HOME/.local/bin/claude}" -p "Call the pick_up tool with item SHIP-1573, then run the shell command: ls /nonexistent-dir . After that say done in one word." \
    --mcp-config ../mcp.json --strict-mcp-config --allowedTools "Bash(ls:*)" "mcp__shiplean__pick_up" --max-turns 6 --model haiku </dev/null)
  sleep 4
  curl -s -u "$ch_auth" "$ch_url" --data-binary "select ResourceAttributes['service.version'], SpanAttributes['session.id'] from otel_traces where ResourceAttributes['run.tag']='$tag' limit 1 format TSV"
}

export_run() {
  local session="${1:?session id}" out="${2:?out dir}"
  CH_URL="$ch_url" CH_AUTH="$ch_auth" python3 - "$session" "$out" <<'PY'
import sys, os, json, re, base64, urllib.request
from datetime import datetime, timezone, timedelta
session, out = sys.argv[1:3]
def ch(q):
    req = urllib.request.Request(os.environ["CH_URL"], q.encode() + b" FORMAT JSONEachRow")
    req.add_header("Authorization", "Basic " + base64.b64encode(os.environ["CH_AUTH"].encode()).decode())
    return [json.loads(l) for l in urllib.request.urlopen(req).read().decode().splitlines() if l]
q = session.replace("'", "")
spans = ch(f"select * from otel_traces where SpanAttributes['session.id']='{q}' order by Timestamp, SpanId")
logs = ch(f"select * from otel_logs where LogAttributes['session.id']='{q}' order by Timestamp, Body, SpanId")
def ts(s):  # ClickHouse DateTime64(9) text -> ns since epoch
    d, f = s.split(".") if "." in s else (s, "0")
    base = datetime.strptime(d, "%Y-%m-%d %H:%M:%S").replace(tzinfo=timezone.utc)
    return int(base.timestamp()) * 10**9 + int(f.ljust(9, "0")[:9])
t0 = min([ts(s["Timestamp"]) for s in spans] + [ts(l["Timestamp"]) for l in logs])
anchor = int(datetime(2026, 11, 10, 10, 0, 0, tzinfo=timezone.utc).timestamp()) * 10**9
shift = anchor - t0
SESSION = "sess-cc-recorded"
DROP = {"run.tag", "anthropic.plan", "host.arch", "os.version", "wsl.version"}
FIXED = {"user.email": "dev@example.com", "user.id": "user-0001", "user.account_id": "account-0001", "user.account_uuid": "account-0001",
         "organization.id": "org-0001", "session.id": SESSION, "gen_ai.conversation.id": SESSION}
ids = {}
def alias(prefix, v):
    return ids.setdefault((prefix, v), f"{prefix}-{len([k for k in ids if k[0] == prefix]) + 1:04d}")
ID_KEYS = {"request_id": "req", "client_request_id": "client-req", "gen_ai.response.id": "resp", "message.uuid": "msg", "prompt.id": "prompt",
          "tool_use_id": "toolu", "gen_ai.tool.call.id": "toolu"}  # both tool keys share one prefix, so the log-to-span join by tool_use_id survives
def fmt_ns(n):  # ISO with ms like the product writes event.timestamp
    return datetime.fromtimestamp(n // 10**9, timezone.utc).strftime("%Y-%m-%dT%H:%M:%S") + f".{(n % 10**9) // 10**6:03d}Z"
def red(k, v):
    if k in FIXED: return FIXED[k]
    if k in ID_KEYS: return alias(ID_KEYS[k], v)
    if k == "event.timestamp": return fmt_ns(int(datetime.strptime(v[:19], "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc).timestamp()) * 10**9 + int(v[20:23]) * 10**6 + shift)
    return v
def clean(attrs): return {k: red(k, v) for k, v in attrs.items() if k not in DROP}
INT_RE = re.compile(r"^-?\d+$")
def typed(k, v):
    if v in ("true", "false"): return {"boolValue": v == "true"}
    if INT_RE.match(v) and not re.search(r"(id|hash|uuid)$", k) and len(v) < 15: return {"intValue": v}
    return {"stringValue": v}
def kv(attrs): return [{"key": k, "value": typed(k, v)} for k, v in sorted(attrs.items())]
def hex_id(h): return h
res = clean(spans[0]["ResourceAttributes"] if spans else logs[0]["ResourceAttributes"])
resource = {"attributes": kv(res)}
kinds = {"Internal": 1, "Server": 2, "Client": 3, "Producer": 4, "Consumer": 5}
otlp_spans, scopes = [], {}
raw_spans = []
for s in spans:
    a = clean(s["SpanAttributes"]); start = ts(s["Timestamp"]) + shift; end = start + int(s["Duration"])
    code = {"Error": 2, "Ok": 1}.get(s["StatusCode"], 0)
    ev = [{"timeUnixNano": str(ts(t) + shift), "name": n, "attributes": kv(clean(at))} for t, n, at in zip(s["Events.Timestamp"], s["Events.Name"], s["Events.Attributes"])]
    o = {"traceId": s["TraceId"], "spanId": s["SpanId"], "parentSpanId": s["ParentSpanId"], "name": s["SpanName"], "kind": kinds.get(s["SpanKind"], 1),
         "startTimeUnixNano": str(start), "endTimeUnixNano": str(end), "attributes": kv(a), "status": {"code": code}}
    if s["StatusMessage"]: o["status"]["message"] = s["StatusMessage"]
    if ev: o["events"] = ev
    scopes.setdefault(s["ScopeName"], {"spans": [], "ver": s["ScopeVersion"]})["spans"].append(o)
    raw_spans.append((s, a, start, end, code, ev))
log_scopes, raw_logs = {}, []
for l in logs:
    a = clean(l["LogAttributes"]); t = ts(l["Timestamp"]) + shift
    o = {"timeUnixNano": str(t), "severityNumber": int(l["SeverityNumber"]), "body": {"stringValue": l["Body"]}, "attributes": kv(a)}
    if l["SeverityText"]: o["severityText"] = l["SeverityText"]
    if l["TraceId"]: o["traceId"] = l["TraceId"]
    if l["SpanId"]: o["spanId"] = l["SpanId"]
    log_scopes.setdefault(l["ScopeName"], {"logs": [], "ver": l["ScopeVersion"]})["logs"].append(o)
    raw_logs.append((l, a, t))
otlp = {"resourceSpans": [{"resource": resource, "scopeSpans": [{"scope": {"name": n, "version": v["ver"]}, "spans": v["spans"]} for n, v in scopes.items()]}],
        "resourceLogs": [{"resource": resource, "scopeLogs": [{"scope": {"name": n, "version": v["ver"]}, "logRecords": v["logs"]} for n, v in log_scopes.items()]}]}
# expected.json (D33: severity number 0 with no text is UNSPECIFIED, not TRACE): the RawRun K3 describes (attributes = resource merged under the record's own, microsecond times)
def iso(n):
    d = datetime.fromtimestamp(n // 10**9, timezone.utc).strftime("%Y-%m-%dT%H:%M:%S")
    us = (n % 10**9) // 1000
    return d + ("." + f"{us:06d}".rstrip("0") if us else "") + "Z"
def sev(n): return "UNSPECIFIED" if n == 0 else "FATAL" if n >= 21 else "ERROR" if n >= 17 else "WARN" if n >= 13 else "INFO" if n >= 9 else "DEBUG" if n >= 5 else "TRACE"
exp_spans = []
for s, a, start, end, code, ev in raw_spans:
    merged = {**res, **a}
    exp_spans.append({"traceId": s["TraceId"], "spanId": s["SpanId"], "parentSpanId": s["ParentSpanId"] or None, "name": s["SpanName"], "kind": "internal",
        "start": iso(start - start % 1000), "end": iso(end - end % 1000), "status": {2: "error", 1: "ok"}.get(code, "unset"), "statusMessage": s["StatusMessage"] or None,
        "attributes": merged, "events": [{"time": iso(int(e["timeUnixNano"])), "name": e["name"], "attributes": {x["key"]: next(iter(x["value"].values())) for x in e["attributes"]}} for e in ev]})
exp_logs = []
for l, a, t in raw_logs:
    merged = {**res, **a}
    exp_logs.append({"time": iso(t - t % 1000), "severity": l["SeverityText"].upper() or sev(int(l["SeverityNumber"])), "body": l["Body"], "attributes": merged,
        "traceId": l["TraceId"] or None, "spanId": l["SpanId"] or None, "sessionId": merged.get("session.id"), "promptId": merged.get("prompt.id"), "eventName": merged.get("event.name")})
span_tok_in = [int(x["attributes"]["input_tokens"]) for x in exp_spans if "input_tokens" in x["attributes"]]
span_tok_out = [int(x["attributes"]["output_tokens"]) for x in exp_spans if "output_tokens" in x["attributes"]]
models = sorted({x["attributes"]["model"] for x in exp_spans + exp_logs if x["attributes"].get("model")})
starts = [r[2] - r[2] % 1000 for r in raw_spans] + [r[2] - r[2] % 1000 for r in raw_logs]  # nanoseconds, not ISO strings
ends = [r[3] - r[3] % 1000 for r in raw_spans] + [r[2] - r[2] % 1000 for r in raw_logs]
errors = sum(1 for x in exp_spans if x["status"] == "error") + sum(1 for x in exp_logs if x["severity"] == "ERROR")
summary = {"runId": SESSION, "sessionId": SESSION, "agent": res["service.name"], "startedBy": res.get("user.email") or exp_spans[0]["attributes"].get("user.email"),
    "models": models, "startedAt": iso(min(starts)), "endedAt": iso(max(ends)), "spanCount": len(exp_spans), "logCount": len(exp_logs), "errorCount": errors,
    "tokensIn": sum(span_tok_in) if span_tok_in else None, "tokensOut": sum(span_tok_out) if span_tok_out else None,
    "links": [{"itemKey": res["shiplean.item"], "source": "tagged"}]}
compare = sorted({"session.id", "prompt.id", "shiplean.item", "shiplean.project", "attempt", "duration_ms", "input_tokens", "output_tokens", "model", "success",
                  "tool_name", "tool_name_safe", "ttft_ms", "decision", "stop_reason", "error_class", "span.type", "tool_use_id"})
expected = {"compareKeys": compare, "run": {"summary": summary, "spans": exp_spans, "logs": exp_logs, "truncated": False}}
blob = json.dumps(otlp) + json.dumps(expected)
import getpass, socket, subprocess
def _git(*a):
    try: return subprocess.run(["git", *a], capture_output=True, text=True).stdout.strip()
    except OSError: return ""
email = _git("config", "user.email")
local = {os.environ.get("USER", ""), getpass.getuser(), os.path.basename(os.path.expanduser("~")), socket.gethostname(), socket.gethostname().split(".")[0],
         email, email.split("@")[0], _git("config", "user.name"), os.path.expanduser("~")}
deny = ["/home", "sk-ant", "Bearer"] + sorted(x for x in local if len(x) >= 3)  # built at run time; nothing personal is committed
low = blob.lower()
for bad in deny:
    assert bad.lower() not in low, "redaction missed a local identity string"
assert not [m for m in re.findall(r"[\w.+-]+@[\w.-]+", blob) if m != "dev@example.com"], "an email is left"
os.makedirs(out, exist_ok=True)
json.dump(otlp, open(os.path.join(out, "claude-code-recorded.otlp.json"), "w"), indent=2); open(os.path.join(out, "claude-code-recorded.otlp.json"), "a").write("\n")
json.dump(expected, open(os.path.join(out, "claude-code-recorded.expected.json"), "w"), indent=2); open(os.path.join(out, "claude-code-recorded.expected.json"), "a").write("\n")
print(f"{len(exp_spans)} spans, {len(exp_logs)} logs, errors {errors}")
PY
}

case "${1:-}" in
  run) run "${2:-}" ;;
  export) export_run "${2:-}" "${3:-}" ;;
  *) echo "usage: $0 run <scratch-dir> | export <session-id> <out-dir>" >&2; exit 2 ;;
esac
