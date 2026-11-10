// Which result a command gets once each call has been checked and turned into actions: nothing usable is one
// failure, some usable is a preview with the rest rejected (partial, D10), and no calls at all is unclear (D9).
import type { AppAction } from '../../actions/types';
import type { ParsedCall, ResolveResult, Transcript, VoiceFailure } from '../contracts';

// What one call, or a group of calls, came to, in the order they were said.
export type Outcome<A extends AppAction> =
  | { ok: true; result: Extract<ResolveResult<A>, { ok: true }> }
  | { ok: false; call: ParsedCall; failure: VoiceFailure };

export const unclear = (transcript: Transcript): VoiceFailure => ({
  kind: 'unclear',
  detail: { heard: transcript.text },
});

// Fills what the spec couldn't know: where the unmatched words are in the transcript, and which action had no
// such state.
export function complete(
  failure: VoiceFailure,
  call: ParsedCall,
  transcript: Transcript,
): VoiceFailure {
  switch (failure.kind) {
    case 'noMatch': {
      if (failure.detail.span !== null) return failure;
      const spoken = failure.detail.spoken;
      const start =
        spoken === '' ? -1 : transcript.text.toLowerCase().indexOf(spoken.toLowerCase());
      if (start === -1) return failure;
      return {
        ...failure,
        detail: { ...failure.detail, span: { start, end: start + spoken.length } },
      };
    }
    case 'noValidAction':
      return failure.detail.action !== null
        ? failure
        : { ...failure, detail: { ...failure.detail, action: call.action } };
    default:
      return failure;
  }
}

// Two lines with the same id (two notes on one item) get #2, #3… so removing one line removes one action.
function uniqueIds<L extends { id: string }>(lines: L[]): L[] {
  const seen = new Map<string, number>();
  return lines.map((l) => {
    const n = (seen.get(l.id) ?? 0) + 1;
    seen.set(l.id, n);
    return n === 1 ? l : { ...l, id: `${l.id}#${n}` };
  });
}

export function classify<A extends AppAction>(
  outcomes: Outcome<A>[],
  transcript: Transcript,
): ResolveResult<A> {
  const completed = (r: { call: ParsedCall; failure: VoiceFailure }) => ({
    call: r.call,
    failure: complete(r.failure, r.call, transcript),
  });
  const failed = outcomes.flatMap((o) => (o.ok ? [] : [completed(o)]));
  const done = outcomes.flatMap((o) => (o.ok ? [o.result] : []));
  if (done.length === 0)
    return { ok: false, failure: failed.length > 0 ? failed[0].failure : unclear(transcript) };
  return {
    ok: true,
    // lines[i] describes actions[i]: each result keeps its own pairs, in the order the calls were said.
    actions: done.flatMap((r) => r.actions),
    lines: uniqueIds(done.flatMap((r) => r.lines)),
    rejected: [...failed, ...done.flatMap((r) => r.rejected.map(completed))],
    view: done.find((r) => r.view !== null)?.view ?? null,
  };
}
