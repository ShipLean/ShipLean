// A 409 on an applied voice batch: nothing was applied, so the preview is rebuilt and asked again.
import type { AppAction, Pending, PreviewLine } from '../../actions/types';
import type { VoiceInternals, VoiceUiState } from '../internals';
import { pairs } from '../machine';
import { showApplyFailed } from './applyFailed';
import { rest, sameAction, sameLine, showRebuilt } from './common';

export const CHANGED_HERE = 'Someone else changed this';

// How long the rebuild waits for the screen to read the item again before it gives up and says the change didn't send.
export const REFRESH_WAIT_MS = 10_000;

// Only a batch voice or typing sent from the last preview: the runner reports every 409, the Inbox's taps too. The
// same calls are resolved again against the screen as it is now (the spec's current context()), the rows that read
// differently and the row that was refused are marked, and the lines the person removed stay removed. Nothing is sent
// until the person applies again.
// The 409 comes before the screen has read the item again (its area refreshes on the refusal, after this), so a spec
// that still builds the refused action at the revision it was refused at is stale: the rebuild waits, still applying,
// until the screen renders with the server's item, and only then rebuilds. A screen that hasn't by REFRESH_WAIT_MS
// (offline, or navigated away) gets the apply-failed sheet with the runner's reason instead.
export function onConflict(v: VoiceInternals, batchId: string, lines: PreviewLine[]): void {
  // lines are the batch as the runner describes it; the rows are compared with the preview the person saw instead.
  void lines;
  const last = v.last();
  if (last === null || !last.result.ok) return giveBack(v, batchId);
  const parts = v.runner.pending().filter((p) => p.batchId === batchId);
  if (parts.length === 0 || parts.some((p) => p.origin === 'tap')) return giveBack(v, batchId);
  const { actions } = last.result;
  if (!parts.every((p) => actions.some((a) => sameAction(a, p.action))))
    return giveBack(v, batchId);

  const before = pairs(last.result);
  const sentIds = new Set(
    before.filter((b) => parts.some((p) => sameAction(b.action, p.action))).map((b) => b.line.id),
  );
  const refused = parts.find((p) => p.failure === 'conflict');
  const source = parts[0].origin === 'typed' ? 'typed' : 'voice';
  const { calls, transcript } = last;
  const screen = v.spec()?.screen ?? null;

  const rebuild = () => {
    const result = v.resolveCalls(calls, transcript);
    const shown = showRebuilt(v, result, transcript, source);
    if (shown.kind === 'view') {
      v.set({
        kind: 'failure',
        failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
        heard: transcript.text,
        source,
      });
      return;
    }
    if (shown.kind !== 'preview' || !result.ok) return;
    const now = pairs(result);
    const marks = now
      .filter(({ line, action }) => {
        const old = before.find((b) => b.line.id === line.id)?.line;
        return (old !== undefined && !sameLine(old, line)) || isRefused(refused, action);
      })
      .map(({ line }) => ({ lineId: line.id, note: CHANGED_HERE }));
    const removed = now
      .filter(({ line }) => before.some((b) => b.line.id === line.id) && !sentIds.has(line.id))
      .map(({ line }) => line.id);
    v.set({ ...shown.state, marks, removed });
  };

  // The screen holds the server's item once the refused action no longer comes out at the revision it was refused at.
  const fresh = (): boolean => {
    const stale = refused === undefined ? null : revisionOf(refused.action);
    if (stale === null) return true;
    const spec = v.spec();
    if (spec === null || spec.screen !== screen) return false;
    const probe = spec.toActions(calls, transcript);
    if (!probe.ok) return true;
    const again = probe.actions.find((a) => isRefused(refused, a));
    return again === undefined || revisionOf(again) !== stale;
  };

  if (fresh()) {
    rebuild();
    return;
  }
  const waiting: VoiceUiState = { kind: 'applying', batchId };
  v.set(waiting);
  let done = false;
  const finish = () => {
    done = true;
    stop();
    clearTimeout(timer);
  };
  const stop = v.watchSpec(() => {
    if (done) return;
    if (v.state() !== waiting) return finish();
    if (!fresh()) return;
    finish();
    rebuild();
  });
  const timer = setTimeout(() => {
    if (done) return;
    finish();
    if (v.state() !== waiting) return;
    showApplyFailed(
      v,
      batchId,
      v.runner.pending().filter((p) => p.batchId === batchId),
    );
  }, REFRESH_WAIT_MS);
}

// A conflict this flow doesn't rebuild: if the provider is still applying the batch, it rests.
function giveBack(v: VoiceInternals, batchId: string): void {
  const s = v.state();
  if (s.kind === 'applying' && s.batchId === batchId) void rest(v, s);
}

// The item revision an action was built at (its expectedRevision), when it carries one.
function revisionOf(a: AppAction): number | null {
  const r = (a as { revision?: unknown }).revision;
  return typeof r === 'number' ? r : null;
}

// The rebuilt action for the refused one: the same kind on the same item (its revision and from value are new).
function isRefused(refused: Pending | undefined, a: Pending['action']): boolean {
  if (refused === undefined) return false;
  const r = refused.action;
  return r.area === a.area && r.kind === a.kind && r.itemId === a.itemId && r.key === a.key;
}
