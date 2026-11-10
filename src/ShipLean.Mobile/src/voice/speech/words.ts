import type { Transcript } from '../contracts';

// The recognizer's tokens and their start times to words with start and end in milliseconds.
export function toWords(tokens: string[], timestamps: number[]): Transcript['words'] {
  void tokens;
  void timestamps;
  throw new Error('TODO: words with timings from tokens');
}
