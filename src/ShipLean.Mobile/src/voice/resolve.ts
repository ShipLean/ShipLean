// Turning what was said into values on the screen. These matchers are exact: case, space and hyphen tolerant, never
// fuzzy. Fuzzy and sound-alike matching (misheard) replace their bodies later, keeping the signatures. Which failure
// a call gets is decided only here.
import type { AppAction } from '../actions/types';
import type {
  Match,
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  VoiceFailure,
} from './contracts';

// "ship 906", "Ship-906" and "SHIP906" all compare as "ship906".
const squash = (s: string) => s.toLowerCase().replace(/[\s-]+/g, '');
// "Mary-Ann  Lee" compares as "mary ann lee".
const words = (s: string) =>
  s
    .toLowerCase()
    .replace(/[\s-]+/g, ' ')
    .trim();

function only<T>(found: T[]): Match<T> {
  return found.length === 1 ? { kind: 'match', value: found[0] } : { kind: 'none' };
}

export function matchKey(spoken: string, candidates: string[]): Match<string> {
  const heard = squash(spoken);
  if (heard === '') return { kind: 'none' };
  return only(candidates.filter((c) => squash(c) === heard));
}

// The full name, or a first name only one person has.
export function matchPerson<P extends { id: string; name: string }>(
  spoken: string,
  people: P[],
): Match<P> {
  const heard = words(spoken);
  if (heard === '') return { kind: 'none' };
  const full = people.filter((p) => words(p.name) === heard);
  if (full.length > 0) return only(full);
  return only(people.filter((p) => words(p.name).split(' ')[0] === heard));
}

export function matchState<S extends { id: string; name: string }>(
  spoken: string,
  states: S[],
): Match<S> {
  const heard = squash(spoken);
  if (heard === '') return { kind: 'none' };
  return only(states.filter((s) => squash(s.name) === heard));
}

// A note is never reworded: the exact characters of the transcript in the span, or null for a span outside it.
export function copyNoteSpan(
  transcript: Transcript,
  span: { start: number; end: number },
): string | null {
  const { start, end } = span;
  if (!Number.isInteger(start) || !Number.isInteger(end)) return null;
  if (start < 0 || end > transcript.text.length || start >= end) return null;
  return transcript.text.slice(start, end);
}

function suggestionText(suggestion: unknown): string {
  if (typeof suggestion === 'string') return suggestion;
  if (typeof suggestion === 'object' && suggestion !== null && 'name' in suggestion)
    return String((suggestion as { name: unknown }).name);
  return String(suggestion);
}

// The one way a spec hands back a value it couldn't match.
export function unmatched(spoken: string, role: string, m: Match<unknown>): VoiceFailure {
  switch (m.kind) {
    case 'none':
      return { kind: 'noMatch', detail: { spoken, role, span: null } };
    case 'misheard':
      return { kind: 'misheardKey', detail: { spoken, suggestion: suggestionText(m.suggestion) } };
    case 'match':
      throw new Error(`unmatched was given a match for ${role}.`);
  }
}

export function resolveCommand<A extends AppAction>(
  spec: ScreenVoiceSpec<A>,
  calls: ParsedCall[],
  transcript: Transcript,
): ResolveResult<A> {
  // TODO: validation and failures (unknown actions, bad arguments, web-only actions, nothing to do) before the spec.
  return spec.toActions(calls, transcript);
}
