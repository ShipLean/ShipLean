import { describe, expect, it } from 'vitest';
import type { VoiceUiState } from '../internals';
import { commandMode } from './commandMode';

const disabled = (
  reason: Extract<VoiceUiState, { kind: 'disabled' }>['reason'],
): VoiceUiState => ({ kind: 'disabled', reason, permission: 'granted' });

describe('commandMode', () => {
  it('derives Preview and the preview-first line', () => {
    const mode = commandMode({ kind: 'idle', permission: 'granted' });
    expect(mode.send).toBe('Preview');
    expect(mode.route.lead + mode.route.rest).toBe(
      'Preview first · nothing changes until you apply',
    );
    expect(mode.route.lead).toBe('Preview first');
    expect(mode.greyed).toBeNull();
  });

  it('greys Command while downloading, skipped or without models, but not when the mic is off', () => {
    for (const reason of ['downloading', 'skipped', 'noModels'] as const) {
      expect(commandMode(disabled(reason)).greyed).toBe('Command needs the voice models');
    }
    expect(commandMode(disabled('micOff')).greyed).toBeNull();
    expect(commandMode({ kind: 'idle', permission: 'deniedFinal' }).greyed).toBeNull();
  });
});
