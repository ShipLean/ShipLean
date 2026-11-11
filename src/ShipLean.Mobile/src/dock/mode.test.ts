import { describe, expect, it } from 'vitest';
import type { DisabledReason, VoiceUiState } from '../voice/internals';
import { modeView } from './mode';

const idle: VoiceUiState = { kind: 'idle', permission: 'granted' };
const disabled = (reason: DisabledReason): VoiceUiState => ({
  kind: 'disabled',
  reason,
  permission: 'granted',
});

describe('modeView', () => {
  it('derives Post and the word-for-word line from note', () => {
    const view = modeView('note', idle);
    expect(view.mode).toBe('note');
    expect(view.send).toBe('Post');
    expect(view.route).toEqual({
      bold: 'Posts when you send',
      rest: ', word for word, like a comment on the web',
    });
    expect(view.segments.command.enabled).toBe(true);
    expect(view.commandOff).toBeNull();

    const command = modeView('command', idle);
    expect(command.mode).toBe('command');
    expect(command.send).toBeNull();
    expect(command.route).toBeNull();
  });

  it('greys Command while downloading, skipped or without models, but not when the mic is off', () => {
    for (const reason of ['downloading', 'skipped', 'noModels'] as const) {
      const view = modeView('command', disabled(reason));
      expect(view.segments.command.enabled).toBe(false);
      expect(view.commandOff).toBe('Command needs the voice models');
      // A greyed Command can't be the mode: the composer stays on Note, with Note's line and Post.
      expect(view.mode).toBe('note');
      expect(view.send).toBe('Post');
    }
    const micOff = modeView('command', disabled('micOff'));
    expect(micOff.segments.command.enabled).toBe(true);
    expect(micOff.commandOff).toBeNull();
    expect(micOff.mode).toBe('command');
  });
});
