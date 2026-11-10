// What the flows share: going back to rest, and showing a rebuilt command as a preview or a failure.
import type { AppAction, PreviewLine } from '../../actions/types';
import type { ResolveResult, Transcript } from '../contracts';
import type { VoiceInternals, VoiceSource, VoiceUiState, VoiceUiStateOf } from '../internals';
import { restingState, turnOf } from '../machine';

// Closes whatever `from` showed: idle, or disabled when the models or the mic say so. Nothing happens when the state
// has moved on from `from` while the mic was read.
export async function rest(v: VoiceInternals, from: VoiceUiState): Promise<void> {
  const permission = await v.engines().mic.status();
  if (v.state() !== from) return;
  v.set(restingState(v.engines().models.status(), permission));
}

export type Shown =
  { kind: 'preview'; state: VoiceUiStateOf<'preview'> } | { kind: 'failure' } | { kind: 'view' };

// Shows a rebuilt command's failure, or returns its preview for the caller to mark and show. A rebuilt command that
// only views something is handed back too: the provider routes views, so the caller resolves it there.
export function showRebuilt(
  v: VoiceInternals,
  result: ResolveResult,
  t: Transcript,
  source: VoiceSource,
): Shown {
  const spec = v.spec();
  if (!result.ok || spec === null) {
    const failure = result.ok
      ? ({ kind: 'noValidAction', detail: { action: null, reason: 'none' } } as const)
      : result.failure;
    v.set({ kind: 'failure', failure, heard: t.text, source });
    return { kind: 'failure' };
  }
  const turn = turnOf(spec, result, t.text, source);
  switch (turn.kind) {
    case 'preview':
      return turn;
    case 'failure':
      v.set({ kind: 'failure', failure: turn.failure, heard: t.text, source });
      return { kind: 'failure' };
    case 'view':
      return { kind: 'view' };
  }
}

// The same action: the runner keeps the dispatched objects, and an outbox reload keeps their fields in order.
export function sameAction(a: AppAction, b: AppAction): boolean {
  return a === b || JSON.stringify(a) === JSON.stringify(b);
}

// The words of `before` that changed in `after`: the common words at both ends are left out, so "Approve D10" against
// "Approve D11" gives "D10". The whole old text when nothing is left (a row that only lost words at an end).
export function changedWords(before: string, after: string): string {
  const a = before.split(/\s+/).filter((w) => w !== '');
  const b = after.split(/\s+/).filter((w) => w !== '');
  let head = 0;
  while (head < a.length && head < b.length && a[head] === b[head]) head++;
  let tail = 0;
  while (
    tail < a.length - head &&
    tail < b.length - head &&
    a[a.length - 1 - tail] === b[b.length - 1 - tail]
  )
    tail++;
  const left = a.slice(head, a.length - tail).join(' ');
  return left === '' ? before : left;
}

export function sameLine(a: PreviewLine, b: PreviewLine): boolean {
  return a.text === b.text && a.detail === b.detail;
}
