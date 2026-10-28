#!/usr/bin/env node
// Smoke test for ShipLean's Linear-compatible endpoint (/linear/graphql) with Linear's own SDK, the way an agent uses it:
// apiUrl pointed at ShipLean, apiKey = the connection's callback token. On the session id it is given it posts a thought,
// an action, an externalUrls update and a response (which completes the session, so use a session you can spare).
//
//   node scripts/linear-sdk-smoke.mjs <base-url> <callback-token> <agent-session-id>
//   e.g. node scripts/linear-sdk-smoke.mjs http://localhost:5000 "$TOKEN" 0192...
//
// Needs Node 20+. It imports @linear/sdk, pinned below to the version published with linear/linear commit 618a737a (the one
// Fixtures/linear-sdk was extracted from). Nothing in the repo installs it, so run it from a temp dir that has it:
//   d=$(mktemp -d) && (cd "$d" && npm init -y >/dev/null && npm i @linear/sdk@99.0.0) \
//     && SDK_DIR="$d/node_modules" node scripts/linear-sdk-smoke.mjs <base-url> <token> <session-id>
// (ES modules ignore NODE_PATH, so the script resolves the package itself, from SDK_DIR, then NODE_PATH, then the current dir.)
import { createRequire } from "node:module";
import path from "node:path";

const SDK_VERSION = "99.0.0";

const [baseUrl, token, sessionId] = process.argv.slice(2);
if (!baseUrl || !token || !sessionId) {
  console.error("usage: node scripts/linear-sdk-smoke.mjs <base-url> <callback-token> <agent-session-id>");
  process.exit(2);
}

const roots = [process.env.SDK_DIR, ...(process.env.NODE_PATH ?? "").split(path.delimiter), process.cwd()].filter(Boolean);
let sdk;
for (const root of roots) {
  try {
    const resolveFrom = createRequire(path.join(path.resolve(root), "noop.js"));
    sdk = await import(resolveFrom.resolve("@linear/sdk"));
    break;
  } catch {
    // try the next place
  }
}
if (!sdk) {
  console.error(`@linear/sdk@${SDK_VERSION} not found. Install it in a temp dir and point NODE_PATH or SDK_DIR at its node_modules (see the header).`);
  process.exit(2);
}

const { LinearClient, AgentActivityType } = sdk;
const client = new LinearClient({ apiUrl: new URL("/linear/graphql", baseUrl).toString(), apiKey: token });

function check(label, payload) {
  if (!payload?.success) {
    throw new Error(`${label} did not succeed: ${JSON.stringify(payload)}`);
  }
  console.log(`ok  ${label}`);
}

const viewer = await client.viewer;
console.log(`ok  viewer ${viewer.id} (${viewer.name})`);

check("thought", await client.createAgentActivity({ agentSessionId: sessionId, content: { type: AgentActivityType.Thought, body: "Reading the item" } }));
check("action", await client.createAgentActivity({
  agentSessionId: sessionId,
  content: { type: AgentActivityType.Action, action: "Run", parameter: "dotnet test", result: "passed" },
}));
check("externalUrls", await client.updateAgentSession(sessionId, {
  externalUrls: [{ label: "Smoke test", url: "https://example.com/linear-sdk-smoke" }],
}));
check("response", await client.createAgentActivity({ agentSessionId: sessionId, content: { type: AgentActivityType.Response, body: "Smoke test finished" } }));
console.log("done");
