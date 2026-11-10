// A 409 on an applied voice batch: nothing was applied, so the preview is rebuilt and asked again.
import type { PreviewLine } from '../../actions/types';
import type { VoiceInternals } from '../internals';

// TODO: rebuild the preview from the items as they are now, mark the rows that changed and ask again.
export function onConflict(v: VoiceInternals, batchId: string, lines: PreviewLine[]): void {
  void v;
  void batchId;
  void lines;
}
