// Fix: holding Fix records a correction to the preview, not a new command.
import type { PreviewLine } from '../../actions/types';
import type { Transcript } from '../contracts';
import type { VoiceInternals, VoiceUiStateOf } from '../internals';
import { requestComposer } from '../typed/composerRequest';
import { isRejectedLine } from '../machine';
import { changedWords, rest, sameLine } from './common';

// Marks the preview as fixing, so the next hold is the correction. A typed preview has Edit instead: the composer
// opens again with the text, and the preview closes.
export function startFix(v: VoiceInternals): void {
  const s = v.state();
  if (s.kind !== 'preview') return;
  if (s.source === 'typed') {
    requestComposer({ mode: 'command', text: s.heard, reason: 'edit', questionNumber: null });
    void rest(v, s);
    return;
  }
  v.set({ ...s, fixing: true });
}

// "start over" at the start of a correction drops the preview; the words after it are a new command.
const START_OVER = /^\s*start\s+over\b[\s,.!?]*/i;

// The correction is parsed with what was heard before it, as one transcript, so the model returns the whole changed
// command. Only the rows that changed are marked "Fixed · was …", and the heard text shows both sentences.
export async function fixHeard(v: VoiceInternals, t: Transcript): Promise<void> {
  const before = v.state();
  if (before.kind !== 'preview') return;
  const over = START_OVER.exec(t.text);
  if (over !== null) {
    const fresh = afterStartOver(t, over[0].length);
    if (fresh.text === '') await rest(v, before);
    else await v.resolve(fresh, { source: before.source });
    return;
  }
  const heard = v.last()?.transcript ?? { text: before.heard, words: [], durationMs: 0 };
  await v.resolve(joined(heard, t), { source: before.source });
  const after = v.state();
  if (after.kind !== 'preview') return;
  v.set({ ...after, marks: fixMarks(before, after.lines) });
}

// The heard words, then the correction's, timed one after the other.
function joined(a: Transcript, b: Transcript): Transcript {
  return {
    text: `${a.text} ${b.text}`,
    words: [
      ...a.words,
      ...b.words.map((w) => ({
        ...w,
        startMs: w.startMs + a.durationMs,
        endMs: w.endMs + a.durationMs,
      })),
    ],
    durationMs: a.durationMs + b.durationMs,
  };
}

function afterStartOver(t: Transcript, cut: number): Transcript {
  const text = t.text.slice(cut).trim();
  const dropped = t.text
    .slice(0, cut)
    .split(/\s+/)
    .filter((w) => w !== '').length;
  return { text, words: t.words.slice(dropped), durationMs: t.durationMs };
}

// A row the same as before keeps its marks. A changed row is paired with an old row by id, else with the next
// unpaired old row in order, and marked "Fixed · was <the old words>"; a row with nothing to pair is marked "Fixed".
function fixMarks(
  before: VoiceUiStateOf<'preview'>,
  lines: PreviewLine[],
): { lineId: string; note: string }[] {
  const shown = lines.filter((l) => !isRejectedLine(l.id));
  const left = before.lines.filter((l) => !isRejectedLine(l.id));
  const unchanged = new Set<string>();
  for (const line of shown) {
    const same = left.filter((o) => sameLine(o, line));
    const match = same.find((o) => o.id === line.id) ?? same[0];
    if (match === undefined) continue;
    left.splice(left.indexOf(match), 1);
    unchanged.add(line.id);
  }
  const marks: { lineId: string; note: string }[] = before.marks.filter((m) =>
    unchanged.has(m.lineId),
  );
  for (const line of shown) {
    if (unchanged.has(line.id)) continue;
    const at = left.findIndex((o) => o.id === line.id);
    const paired = left.splice(at >= 0 ? at : 0, 1)[0];
    marks.push({
      lineId: line.id,
      note: paired === undefined ? 'Fixed' : `Fixed · was ${changedWords(paired.text, line.text)}`,
    });
  }
  return marks;
}
