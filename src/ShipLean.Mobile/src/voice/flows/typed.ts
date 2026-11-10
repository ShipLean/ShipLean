// A typed command goes through the same parse, preview and apply as a spoken one, with source 'typed'.
import type { VoiceInternals } from '../internals';

// TODO: parse the typed text as a command (source 'typed') and show its preview or failure.
export async function submitTyped(v: VoiceInternals, text: string): Promise<void> {
  void v;
  void text;
}
