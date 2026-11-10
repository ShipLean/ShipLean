// A note is never reworded: it is the transcript's own characters in the span the model pointed at, with only the
// spaces around them and a leading spoken "colon" or "that" taken off.
import type { Transcript } from '../contracts';

const WORD = /[\p{L}\p{N}']/u;

const isWord = (c: string | undefined) => c !== undefined && WORD.test(c);

const LEAD = /^(?:[\s:,]+|(?:colon|that)(?![\p{L}\p{N}']))/iu;

// The note's words, or null when the span lies outside the transcript, cuts a word in half or holds no words.
export function noteText(
  transcript: Transcript,
  span: { start: number; end: number },
): string | null {
  const { text } = transcript;
  const { start, end } = span;
  if (!Number.isInteger(start) || !Number.isInteger(end)) return null;
  if (start < 0 || end > text.length || start >= end) return null;
  if (isWord(text[start - 1]) && isWord(text[start])) return null;
  if (isWord(text[end - 1]) && isWord(text[end])) return null;
  let note = text.slice(start, end);
  for (let lead = LEAD.exec(note); lead !== null; lead = LEAD.exec(note))
    note = note.slice(lead[0].length);
  note = note.trim();
  return note === '' ? null : note;
}
