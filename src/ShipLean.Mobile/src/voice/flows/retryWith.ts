// A one-tap correction from a failure sheet: rerun the last command with a span changed, or keep it as a note.
import type { RetryChange, VoiceInternals } from '../internals';

// TODO: rerun the last parse with the span replaced (or the whole heard text as a note) and show the result.
export async function retryWith(v: VoiceInternals, change: RetryChange): Promise<void> {
  void v;
  void change;
}
