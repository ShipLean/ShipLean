// A typed command goes through the same parse, preview and apply as a spoken one, with source 'typed'.
import type { VoiceInternals } from '../internals';

// The recognizer is skipped: the typed text is the transcript, with no words timed and no duration.
export async function submitTyped(v: VoiceInternals, text: string): Promise<void> {
  if (text.trim() === '') return;
  await v.resolve({ text, words: [], durationMs: 0 }, { source: 'typed' });
}
