import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildLogRequest, parseShipleanFile, parseHeaders, run, answerFor, readShipleanFile } from './shiplean-cursor-hook.mjs';

const attr = (list, key) => list.find((a) => a.key === key)?.value.stringValue;
const shell = { hook_event_name: 'afterShellExecution', conversation_id: 'c-1', generation_id: 'g-1', command: 'dotnet test', output: 'passed', duration: 5000 };

async function runWith(payload, env, files = {}) {
  const posts = [];
  const out = [];
  await run({
    stdin: JSON.stringify(payload),
    env,
    cwd: '/work/repo',
    fetchFn: async (url, init) => { posts.push({ url, init, body: JSON.parse(init.body) }); return { ok: true }; },
    out: (s) => out.push(s),
  });
  return { posts, out };
}

test('record tags item, project and link_source file, with the session, prompt, tool and duration', () => {
  const body = buildLogRequest(shell, { item: 'SHIP-1', project: 'SHIP', email: 'dev@example.com', now: 1794304809000 });
  const resource = body.resourceLogs[0].resource.attributes;
  const record = body.resourceLogs[0].scopeLogs[0].logRecords[0];
  assert.equal(attr(resource, 'service.name'), 'cursor');
  assert.equal(attr(resource, 'shiplean.item'), 'SHIP-1');
  assert.equal(attr(resource, 'shiplean.project'), 'SHIP');
  assert.equal(attr(resource, 'shiplean.link_source'), 'file');
  assert.equal(attr(resource, 'user.email'), 'dev@example.com');
  assert.equal(record.body.stringValue, 'afterShellExecution');
  assert.equal(record.timeUnixNano, '1794304809000000000');
  assert.equal(attr(record.attributes, 'event.name'), 'cursor.hook.afterShellExecution');
  assert.equal(attr(record.attributes, 'session.id'), 'c-1');
  assert.equal(attr(record.attributes, 'prompt.id'), 'g-1');
  assert.equal(attr(record.attributes, 'tool_name'), 'shell');
  assert.equal(attr(record.attributes, 'duration_ms'), '5000');
});

test('content stays out unless asked for', () => {
  const off = buildLogRequest(shell, { item: 'SHIP-1' }).resourceLogs[0].scopeLogs[0].logRecords[0].attributes;
  const on = buildLogRequest(shell, { item: 'SHIP-1', withContent: true }).resourceLogs[0].scopeLogs[0].logRecords[0].attributes;
  assert.equal(attr(off, 'output'), undefined);
  assert.equal(attr(off, 'tool_input'), undefined);
  assert.equal(attr(on, 'output'), 'passed');
  assert.equal(attr(on, 'tool_input'), '{"command":"dotnet test"}');
});

test('file edit, MCP call and stop events carry file_path, tool_name and status', () => {
  const rec = (p) => buildLogRequest(p, { item: 'SHIP-1' }).resourceLogs[0].scopeLogs[0].logRecords[0].attributes;
  assert.equal(attr(rec({ hook_event_name: 'afterFileEdit', file_path: 'src/A.cs' }), 'file_path'), 'src/A.cs');
  assert.equal(attr(rec({ hook_event_name: 'afterMCPExecution', tool_name: 'update_progress', duration: 600.4 }), 'tool_name'), 'update_progress');
  assert.equal(attr(rec({ hook_event_name: 'afterMCPExecution', tool_name: 'x', duration: 600.4 }), 'duration_ms'), '600');
  assert.equal(attr(rec({ hook_event_name: 'stop', status: 'completed' }), 'status'), 'completed');
});

test('.shiplean file parses key=value lines and a bare item key', () => {
  assert.deepEqual(parseShipleanFile('# note\nitem = SHIP-7\nproject=SHIP\n'), { item: 'SHIP-7', project: 'SHIP' });
  assert.deepEqual(parseShipleanFile('SHIP-9\n'), { item: 'SHIP-9' });
});

test('.shiplean is found in a parent directory', () => {
  const read = readShipleanFile('/work/repo/src', (p) => p === '/work/repo/.shiplean', () => 'item=SHIP-3');
  assert.deepEqual(read, { item: 'SHIP-3' });
});

test('headers parse as k=v pairs', () => {
  assert.deepEqual(parseHeaders('Authorization=Bearer abc, x-team=a'), { Authorization: 'Bearer abc', 'x-team': 'a' });
});

test('one event on stdin posts one record to /v1/logs with the headers and the env item, and answers allow', async () => {
  const { posts, out } = await runWith(
    { ...shell, hook_event_name: 'beforeShellExecution' },
    { SHIPLEAN_INSPECTION_AGENT_OTLP_ENDPOINT: 'http://collector:4318/', SHIPLEAN_INSPECTION_AGENT_OTLP_HEADERS: 'x-key=k', SHIPLEAN_ITEM: 'SHIP-5', SHIPLEAN_PROJECT: 'SHIP' },
  );
  assert.equal(posts.length, 1);
  assert.equal(posts[0].url, 'http://collector:4318/v1/logs');
  assert.equal(posts[0].init.headers['x-key'], 'k');
  const resource = posts[0].body.resourceLogs[0].resource.attributes;
  assert.equal(attr(resource, 'shiplean.item'), 'SHIP-5');
  assert.equal(posts[0].body.resourceLogs[0].scopeLogs[0].logRecords.length, 1);
  assert.deepEqual(JSON.parse(out[0]), { permission: 'allow' });
});

test('no endpoint posts nothing; a failing post still answers', async () => {
  assert.equal((await runWith(shell, {})).posts.length, 0);
  const out = [];
  await run({ stdin: JSON.stringify(shell), env: { SHIPLEAN_INSPECTION_AGENT_OTLP_ENDPOINT: 'http://x' }, cwd: '/nowhere', fetchFn: async () => { throw new Error('down'); }, out: (s) => out.push(s) });
  assert.deepEqual(JSON.parse(out[0]), {});
});

test('answers fit the event: continue for a prompt, permission for before hooks', () => {
  assert.deepEqual(answerFor('beforeSubmitPrompt'), { continue: true });
  assert.deepEqual(answerFor('beforeMCPExecution'), { permission: 'allow' });
  assert.deepEqual(answerFor('stop'), {});
});
