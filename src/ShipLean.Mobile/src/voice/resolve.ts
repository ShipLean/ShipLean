// Turning what was said into values on the screen. Keys, people and states match loosely (case, spaces, hyphens,
// accents, word forms and small slips of the ear); a key that sounds like one on the screen comes back as misheard
// with that one suggestion. Which failure a call gets is decided only here: specs act on a match and hand anything
// else to unmatched, and resolveCommand checks every call before the spec sees it.
import type { AppAction } from '../actions/types';
import type {
  Match,
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  VoiceFailure,
} from './contracts';
import { classify, unclear, type Outcome } from './resolve/classify';
import { findPerson, findState } from './resolve/fuzzy';
import { noteText } from './resolve/note';
import { suggestKey } from './resolve/soundAlike';
import { readSpokenKey } from './resolve/spokenKey';
import { checkCall } from './resolve/validate';

// "ship 906", "Ship-906" and "SHIP906" all compare as "ship906".
const squash = (s: string) => s.toLowerCase().replace(/[\s-]+/g, '');

const unique = <T>(found: T[]): Match<T> =>
  found.length === 1 ? { kind: 'match', value: found[0] } : { kind: 'none' };

const found = <T>(value: T | null): Match<T> =>
  value === null ? { kind: 'none' } : { kind: 'match', value };

// The key as written or as said ("ship nine oh six"), else the one key on the screen that sounds like it (D6).
export function matchKey(spoken: string, candidates: string[]): Match<string> {
  if (squash(spoken) === '') return { kind: 'none' };
  const said = readSpokenKey(spoken);
  const heard = [squash(spoken), ...(said === null ? [] : [squash(said)])];
  const exact = candidates.filter((c) => heard.includes(squash(c)));
  if (exact.length > 0) return unique([...new Set(exact)]);
  if (said === null) return { kind: 'none' };
  const suggestion = suggestKey(said, candidates);
  return suggestion === null ? { kind: 'none' } : { kind: 'misheard', heard: said, suggestion };
}

// The full name, a first name or a surname only one person has, allowing for accents and a small slip.
export function matchPerson<P extends { id: string; name: string }>(
  spoken: string,
  people: P[],
): Match<P> {
  return found(findPerson(spoken, people));
}

// A state of the item's type (the caller passes that type's states only, D8), by name or another form of it.
export function matchState<S extends { id: string; name: string }>(
  spoken: string,
  states: S[],
): Match<S> {
  return found(findState(spoken, states));
}

// A note is never reworded: the transcript's own characters in the span, trimmed of the spaces around them and a
// leading spoken "colon" or "that". Null for a span outside the transcript or one that cuts a word in half.
export function copyNoteSpan(
  transcript: Transcript,
  span: { start: number; end: number },
): string | null {
  return noteText(transcript, span);
}

function suggestionText(suggestion: unknown): string {
  if (typeof suggestion === 'string') return suggestion;
  if (typeof suggestion === 'object' && suggestion !== null && 'name' in suggestion)
    return String((suggestion as { name: unknown }).name);
  return String(suggestion);
}

// The role a spec names for a state; a state the item's type doesn't have is no valid action, not a missing item.
const STATE_ROLE = 'state';

// The one way a spec hands back a value it couldn't match. resolveCommand fills the span and the action.
export function unmatched(spoken: string, role: string, m: Match<unknown>): VoiceFailure {
  switch (m.kind) {
    case 'none':
      return role === STATE_ROLE
        ? { kind: 'noValidAction', detail: { action: null, reason: 'noSuchState' } }
        : { kind: 'noMatch', detail: { spoken, role, span: null } };
    case 'misheard':
      return { kind: 'misheardKey', detail: { spoken, suggestion: suggestionText(m.suggestion) } };
    case 'match':
      throw new Error(`unmatched was given a match for ${role}.`);
  }
}

// Checks every call (a listed action, one the phone can take, the note from the transcript, arguments that fit),
// gives the spec only the calls that pass, and classifies what comes back. When the spec refuses several calls
// together, each is tried alone, so one bad call leaves the rest as a partial preview.
export function resolveCommand<A extends AppAction>(
  spec: ScreenVoiceSpec<A>,
  calls: ParsedCall[],
  transcript: Transcript,
): ResolveResult<A> {
  if (calls.length === 0) return { ok: false, failure: unclear(transcript) };
  const checked = calls.map((c) => checkCall(spec.actions, c, transcript));
  const passing = checked.flatMap((c) => (c.ok ? [c.call] : []));
  const together = passing.length > 0 ? spec.toActions(passing, transcript) : null;
  const alone = (call: ParsedCall): Outcome<A> => {
    const r =
      passing.length === 1 && together !== null ? together : spec.toActions([call], transcript);
    return r.ok ? { ok: true, result: r } : { ok: false, call, failure: r.failure };
  };
  let joined = false;
  const outcomes = checked.flatMap((c): Outcome<A>[] => {
    if (!c.ok) return [{ ok: false, call: c.call, failure: c.failure }];
    if (together === null || !together.ok) return [alone(c.call)];
    if (joined) return [];
    joined = true;
    return [{ ok: true, result: together }];
  });
  // A refusal of the batch stands even when each call works alone: the spec said no to the combination.
  if (together !== null && !together.ok && outcomes.every((o) => o.ok))
    return classify([{ ok: false, call: passing[0], failure: together.failure }], transcript);
  return classify(outcomes, transcript);
}
