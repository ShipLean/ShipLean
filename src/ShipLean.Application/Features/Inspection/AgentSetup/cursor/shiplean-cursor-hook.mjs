#!/usr/bin/env node
// ShipLean Cursor hook: turns one Cursor hook event (read as JSON on stdin) into one OTLP/HTTP JSON log record and posts it to your
// collector. No dependencies; needs Node 18 or newer (global fetch).
//
// Settings (environment):
//   SHIPLEAN_INSPECTION_AGENT_OTLP_ENDPOINT  collector base URL, e.g. https://otel.example.com:4318 (the /v1/logs path is added)
//   SHIPLEAN_INSPECTION_AGENT_OTLP_HEADERS   optional, "k1=v1,k2=v2" (the OTLP exporter header format)
//   SHIPLEAN_ITEM, SHIPLEAN_PROJECT          the work item and project to tag; else read from a .shiplean file (below)
//   SHIPLEAN_LOG_CONTENT=1                   also send prompt text, tool input and output (off by default; may hold secrets)
//
// A `.shiplean` file in the workspace root (or any parent) holds `item=SHIP-123` and `project=SHIP`, one per line.
//
// The hook never blocks Cursor: it always answers "allow", and a failed post is dropped silently.

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export function parseShipleanFile(text) {
  const found = {};
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (line === '' || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq < 0) {
      if (/^[A-Za-z][A-Za-z0-9]*-\d+$/.test(line)) found.item ??= line;
      continue;
    }
    const key = line.slice(0, eq).trim().toLowerCase();
    const value = line.slice(eq + 1).trim();
    if (value !== '' && (key === 'item' || key === 'project')) found[key] = value;
  }
  return found;
}

export function readShipleanFile(startDir, exists = existsSync, read = (p) => readFileSync(p, 'utf8')) {
  let dir = resolve(startDir);
  for (;;) {
    const candidate = join(dir, '.shiplean');
    if (exists(candidate)) return parseShipleanFile(read(candidate));
    const parent = dirname(dir);
    if (parent === dir) return {};
    dir = parent;
  }
}

export function parseHeaders(text) {
  const headers = {};
  for (const pair of (text ?? '').split(',')) {
    const eq = pair.indexOf('=');
    if (eq > 0) headers[pair.slice(0, eq).trim()] = pair.slice(eq + 1).trim();
  }
  return headers;
}

const str = (key, value) => ({ key, value: { stringValue: String(value) } });

// The attributes one hook event carries. Content (prompt, tool input, output) only when asked for.
export function eventAttributes(payload, withContent) {
  const event = payload.hook_event_name ?? 'unknown';
  const attrs = { 'event.name': `cursor.hook.${event}` };
  if (payload.conversation_id) attrs['session.id'] = payload.conversation_id;
  if (payload.generation_id) attrs['prompt.id'] = payload.generation_id;
  if (event.endsWith('ShellExecution')) attrs.tool_name = 'shell';
  if (payload.tool_name) attrs.tool_name = payload.tool_name;
  if (payload.mcp_server_name) attrs.mcp_server_name = payload.mcp_server_name;
  if (payload.file_path) attrs.file_path = payload.file_path;
  if (payload.status) attrs.status = payload.status;
  if (typeof payload.duration === 'number') attrs.duration_ms = Math.round(payload.duration);
  if (withContent) {
    if (payload.prompt) attrs.prompt = payload.prompt;
    const input = payload.tool_input ?? (payload.command ? { command: payload.command } : undefined);
    if (input !== undefined) attrs.tool_input = typeof input === 'string' ? input : JSON.stringify(input);
    const output = payload.output ?? payload.result_json;
    if (output !== undefined) attrs.output = typeof output === 'string' ? output : JSON.stringify(output);
  }
  return attrs;
}

// The OTLP/HTTP JSON body for one event: resource tags the item, one log record the event. shiplean.link_source "file" is set only
// when the item came from a .shiplean file (itemFromFile); an item from SHIPLEAN_ITEM is a plain tag.
export function buildLogRequest(payload, { item, project, email, repo, branch, itemFromFile = false, withContent = false, now = Date.now() }) {
  const resource = [str('service.name', 'cursor')];
  if (project) resource.push(str('shiplean.project', project));
  if (item) {
    resource.push(str('shiplean.item', item));
    if (itemFromFile) resource.push(str('shiplean.link_source', 'file'));
  }
  if (email) resource.push(str('user.email', email));
  if (repo) resource.push(str('vcs.repository.url.full', repo));
  if (branch) resource.push(str('vcs.ref.head.name', branch));
  if (payload.model) resource.push(str('gen_ai.request.model', payload.model));
  if (payload.cursor_version) resource.push(str('service.version', payload.cursor_version));
  const attributes = Object.entries(eventAttributes(payload, withContent)).map(([k, v]) => str(k, v));
  return {
    resourceLogs: [
      {
        resource: { attributes: resource },
        scopeLogs: [
          {
            scope: { name: 'shiplean-cursor-hook' },
            logRecords: [
              {
                timeUnixNano: String(BigInt(now) * 1000000n),
                severityText: 'INFO',
                severityNumber: 9,
                body: { stringValue: payload.hook_event_name ?? 'unknown' },
                attributes,
              },
            ],
          },
        ],
      },
    ],
  };
}

// Repository URL and branch from git; empty when git is missing or this is not a repository. Credentials in the URL are dropped.
export function gitInfo(cwd, exec = (args) => execFileSync('git', args, { cwd, timeout: 1000, stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim()) {
  const info = {};
  try {
    info.repo = exec(['remote', 'get-url', 'origin']).replace(/\/\/[^/@]*@/, '//') || undefined;
  } catch {
    // no remote
  }
  try {
    const branch = exec(['rev-parse', '--abbrev-ref', 'HEAD']);
    if (branch && branch !== 'HEAD') info.branch = branch;
  } catch {
    // not a repository
  }
  return info;
}

// What Cursor expects back so it carries on: before* hooks need a permission, beforeSubmitPrompt needs continue.
// With no readable event, answer both shapes so no hook can be denied by our output.
export function answerFor(event) {
  if (event === undefined) return { continue: true, permission: 'allow' };
  if (event === 'beforeSubmitPrompt') return { continue: true };
  if (typeof event === 'string' && event.startsWith('before')) return { permission: 'allow' };
  return {};
}

export async function run({ stdin, env, cwd, fetchFn = fetch, out = (s) => process.stdout.write(s) }) {
  let payload = {};
  try {
    payload = JSON.parse(stdin);
  } catch {
    out(JSON.stringify(answerFor(undefined)));
    return;
  }
  try {
    const endpoint = env.SHIPLEAN_INSPECTION_AGENT_OTLP_ENDPOINT;
    if (endpoint) {
      const root = payload.workspace_roots?.[0] ?? cwd;
      const file = readShipleanFile(root);
      const git = payload.vcs ?? gitInfo(root);
      const body = buildLogRequest(payload, {
        item: env.SHIPLEAN_ITEM || file.item,
        itemFromFile: !env.SHIPLEAN_ITEM && Boolean(file.item),
        project: env.SHIPLEAN_PROJECT || file.project,
        repo: git.repo,
        branch: git.branch,
        email: env.SHIPLEAN_USER_EMAIL || payload.user_email,
        withContent: env.SHIPLEAN_LOG_CONTENT === '1',
      });
      await fetchFn(`${endpoint.replace(/\/+$/, '')}/v1/logs`, {
        method: 'POST',
        headers: { 'content-type': 'application/json', ...parseHeaders(env.SHIPLEAN_INSPECTION_AGENT_OTLP_HEADERS) },
        body: JSON.stringify(body),
        signal: AbortSignal.timeout(3000),
      });
    }
  } catch {
    // Telemetry must never get in the way of the agent.
  }
  out(JSON.stringify(answerFor(payload.hook_event_name)));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const chunks = [];
  for await (const chunk of process.stdin) chunks.push(chunk);
  await run({ stdin: Buffer.concat(chunks).toString('utf8'), env: process.env, cwd: process.cwd() });
}
