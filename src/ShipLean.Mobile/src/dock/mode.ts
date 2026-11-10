// The composer's mode decides the switch, the route line and Send's label together (typed D1), so they always agree.
// Pure: everything the composer draws for its mode comes from here, never set one by one.
import type { DisabledReason, VoiceUiState } from '../voice/internals';

export type ComposerMode = 'note' | 'command';

// Command reads the text with the on-device models, so it waits for them (typed D8). Typing needs no mic, so a mic
// that is off leaves Command usable.
const COMMAND_NEEDS_MODELS: readonly DisabledReason[] = ['downloading', 'skipped', 'noModels'];

export interface ModeView {
  // The mode in force: Command falls back to Note while Command can't be used.
  mode: ComposerMode;
  segments: { note: { enabled: true }; command: { enabled: boolean } };
  // Why Command is greyed, shown under the switch; null while it can be used.
  commandOff: string | null;
  // Note's route line ("Posts when you send" in bold, then the rest) and Send's label. Null in Command, whose route
  // line, reason line and Preview belong to CommandComposer.
  route: { bold: string; rest: string } | null;
  send: 'Post' | null;
}

export function commandEnabled(voice: VoiceUiState): boolean {
  return !(voice.kind === 'disabled' && COMMAND_NEEDS_MODELS.includes(voice.reason));
}

export function modeView(chosen: ComposerMode, voice: VoiceUiState): ModeView {
  const enabled = commandEnabled(voice);
  const mode: ComposerMode = chosen === 'command' && enabled ? 'command' : 'note';
  return {
    mode,
    segments: { note: { enabled: true }, command: { enabled } },
    commandOff: enabled ? null : 'Command needs the voice models',
    route:
      mode === 'note'
        ? { bold: 'Posts when you send', rest: ', word for word, like a comment on the web' }
        : null,
    send: mode === 'note' ? 'Post' : null,
  };
}
