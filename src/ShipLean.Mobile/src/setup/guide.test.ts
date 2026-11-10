import type { InboxAskDto, InboxDto, InboxItemDto } from '@shiplean/core/api/inbox';
import type { KeyValueStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import type { VoiceUiState } from '../voice/internals';
import {
  coachVariant,
  firstCommandStep,
  firstResultView,
  markGuide,
  readGuide,
  type GuideFlags,
} from './guide';

const flags = (over: Partial<GuideFlags> = {}): GuideFlags => ({
  voiceReadyShown: false,
  micCoachShown: false,
  firstCommandShown: false,
  fromSetupTipShown: false,
  ...over,
});

function memoryStore(): KeyValueStore {
  const map = new Map<string, string>();
  return {
    getItem: (k) => map.get(k) ?? null,
    setItem: (k, v) => void map.set(k, v),
    removeItem: (k) => void map.delete(k),
  } as KeyValueStore;
}

describe('guide', () => {
  it('keeps each flag on its own', () => {
    const kv = memoryStore();
    markGuide(kv, 'voiceReadyShown');
    expect(readGuide(kv)).toEqual(flags({ voiceReadyShown: true }));
  });

  it('shows the Try it coach mark once ready with the microphone on, and the turn-it-on one first without', () => {
    expect(coachVariant('downloading', 'granted', flags())).toBeNull();
    expect(coachVariant('ready', 'granted', flags())).toBe('try');
    expect(coachVariant('ready', 'deniedFinal', flags())).toBe('micOff');
    expect(coachVariant('ready', 'undetermined', flags())).toBe('micOff');
    expect(coachVariant('ready', 'granted', flags({ voiceReadyShown: true }))).toBeNull();
    expect(coachVariant('ready', 'granted', flags({ firstCommandShown: true }))).toBeNull();
    // Shown once without the microphone; the guided command still waits, and follows once it is allowed.
    expect(coachVariant('ready', 'deniedFinal', flags({ micCoachShown: true }))).toBeNull();
    expect(coachVariant('ready', 'granted', flags({ micCoachShown: true }))).toBe('try');
  });

  it('a non read-only first result leaves the guide pending', () => {
    const shown = flags({ voiceReadyShown: true });
    const preview = { kind: 'preview' } as VoiceUiState;
    const failure = { kind: 'failure' } as VoiceUiState;
    const answer = { kind: 'answer', content: { lead: '', rows: [] } } as VoiceUiState;
    expect(firstCommandStep(shown, preview)).toBe('wait');
    expect(firstCommandStep(shown, failure)).toBe('wait');
    expect(firstCommandStep(shown, answer)).toBe('show');
    // No coach mark yet, or the guide is over: an answer is just an answer.
    expect(firstCommandStep(flags(), answer)).toBe('wait');
    expect(
      firstCommandStep(flags({ voiceReadyShown: true, firstCommandShown: true }), answer),
    ).toBe('wait');
  });
});

const ask = (kind: InboxAskDto['kind']) => ({ kind }) as InboxAskDto;
const item = (key: string, title: string, kinds: InboxAskDto['kind'][]) =>
  ({ key, title, asks: kinds.map(ask) }) as InboxItemDto;

describe('firstResultView', () => {
  it('counts each kind of ask in the Inbox order', () => {
    const inbox = {
      items: [
        item('SHIP-743', 'Nightly backups', ['status']),
        item('SHIP-907', 'Mobile app foundations', ['decision', 'decision', 'question']),
        item('SHIP-742', 'GitHub issues sync', ['mention']),
      ],
    } as InboxDto;
    expect(firstResultView(inbox)).toEqual({
      lead: '3 items wait on you: 2 decisions, 1 question, a mention and a status line to check.',
      rows: [
        { key: 'SHIP-743', title: 'Nightly backups', counts: 'status line' },
        { key: 'SHIP-907', title: 'Mobile app foundations', counts: '2 decisions · 1 question' },
        { key: 'SHIP-742', title: 'GitHub issues sync', counts: '1 mention' },
      ],
    });
  });

  it('says nothing is waiting for an empty Inbox', () => {
    expect(firstResultView({ items: [] } as unknown as InboxDto).lead).toBe(
      'Nothing is waiting on you.',
    );
  });
});
