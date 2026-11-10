// A one-tap correction from a failure sheet: rerun the last command with a span changed, or keep it as a note.
import type { ParsedCall } from '../contracts';
import type { RetryChange, VoiceInternals, VoiceSource } from '../internals';
import { showRebuilt } from './common';

// Reruns the last parse without the model: the calls' arguments that held the spoken words in the span take the
// chosen value (a key, a person or a state), and the heard text stays word for word. { asNote } turns the whole heard
// text into the screen's note action; the spec copies it with copyNoteSpan, so it is never reworded.
export async function retryWith(v: VoiceInternals, change: RetryChange): Promise<void> {
  const last = v.last();
  if (last === null) return;
  const t = last.transcript;
  const source = sourceOf(v);
  let calls: ParsedCall[];
  if ('asNote' in change) {
    const note = v.spec()?.actions.find((a) => a.noteArg !== undefined);
    if (note === undefined) {
      v.set({
        kind: 'failure',
        failure: { kind: 'noValidAction', detail: { action: null, reason: 'notAllowed' } },
        heard: t.text,
        source,
      });
      return;
    }
    calls = [{ action: note.name, args: {}, noteSpan: { start: 0, end: t.text.length } }];
  } else {
    const spoken = t.text.slice(change.span.start, change.span.end);
    calls = last.calls.map((c) => ({ ...c, args: replaced(c.args, spoken, change.value) }));
  }
  const shown = showRebuilt(v, v.resolveCalls(calls, t), t, source);
  if (shown.kind === 'preview') v.set(shown.state);
  // A command that only views something: the provider routes views, so it resolves the heard text there.
  else if (shown.kind === 'view') await v.resolve(t, { source });
}

function sourceOf(v: VoiceInternals): VoiceSource {
  const s = v.state();
  return s.kind === 'failure' || s.kind === 'preview' || s.kind === 'thinking' ? s.source : 'voice';
}

// Every string argument that is the spoken words (ignoring case and spaces at the ends) becomes the value.
function replaced(
  args: Record<string, unknown>,
  spoken: string,
  value: string,
): Record<string, unknown> {
  const swap = (x: unknown): unknown => {
    if (typeof x === 'string') return same(x, spoken) ? value : x;
    if (Array.isArray(x)) return x.map(swap);
    return x;
  };
  return Object.fromEntries(Object.entries(args).map(([k, x]) => [k, swap(x)]));
}

function same(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
}
