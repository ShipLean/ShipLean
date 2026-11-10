// Fix: holding Fix records a correction to the preview, not a new command.
import type { Transcript } from '../contracts';
import type { VoiceInternals } from '../internals';

// TODO: mark the preview as fixing and start listening for the correction.
export function startFix(v: VoiceInternals): void {
  void v;
}

// TODO: give the model the current preview and the correction, change only the rows it touches and mark each
// "Fixed · was …".
export async function fixHeard(v: VoiceInternals, t: Transcript): Promise<void> {
  void v;
  void t;
}
