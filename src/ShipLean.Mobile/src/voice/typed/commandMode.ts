// The dock's Command side, derived from the voice state in one place: Send's label, the route line under the field,
// and whether Command is greyed (typed D5, D8). A typed command is read by the on-device model, so it needs the
// models; a mic that is off doesn't matter to it.
import type { VoiceUiState } from '../internals';

export interface CommandMode {
  // Send's label on Command.
  send: 'Preview';
  // The route line under the field: the lead is bold.
  route: { lead: string; rest: string };
  // Why Command can't be used yet, or null when it can.
  greyed: string | null;
}

const ROUTE = { lead: 'Preview first', rest: ' · nothing changes until you apply' };

// TODO: add the time left from the download progress to the greyed line once the voice state carries it.
const NEEDS_MODELS = 'Command needs the voice models';

export function commandMode(state: VoiceUiState): CommandMode {
  const greyed =
    state.kind === 'disabled' && state.reason !== 'micOff' ? NEEDS_MODELS : null;
  return { send: 'Preview', route: ROUTE, greyed };
}
