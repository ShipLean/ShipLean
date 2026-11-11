import type { Transcript } from '../contracts';

// The recognizer's tokens and their start times to words with start and end in milliseconds.
// Tokens are sentencepiece pieces as sherpa-onnx returns them: a piece that starts a word begins with a space (or
// the "▁" marker), and a piece without one (the rest of a word, or punctuation) joins the word before it. timestamps
// are each token's start in seconds. A word starts at its first token and ends where the next word starts; the last
// word ends one encoder frame after its last token starts, since the recognizer gives no end time.
const WORD_START = /^[\s▁]/;
export const LAST_TOKEN_MS = 80;

export function toWords(tokens: string[], timestamps: number[]): Transcript['words'] {
  if (tokens.length !== timestamps.length) {
    throw new Error(`toWords: ${tokens.length} tokens but ${timestamps.length} timestamps`);
  }
  const words: { w: string; startMs: number; lastTokenMs: number }[] = [];
  tokens.forEach((token, i) => {
    const ms = Math.round(timestamps[i] * 1000);
    const text = token.replace(/[\s▁]+/g, '');
    const current = words.at(-1);
    if (WORD_START.test(token) || !current) {
      words.push({ w: text, startMs: ms, lastTokenMs: ms });
    } else {
      current.w += text;
      current.lastTokenMs = ms;
    }
  });
  return words
    .map((word, i) => ({
      w: word.w,
      startMs: word.startMs,
      endMs: i + 1 < words.length ? words[i + 1].startMs : word.lastTokenMs + LAST_TOKEN_MS,
    }))
    .filter((word) => word.w !== '');
}
