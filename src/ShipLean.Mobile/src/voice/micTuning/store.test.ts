import { describe, expect, it, vi } from 'vitest';
import type { MicTuning } from '../contracts';
import { createMicTuningStore } from './store';

function fakeStorage(initial: Record<string, string> = {}) {
  const data = new Map(Object.entries(initial));
  return {
    getItemSync: (k: string) => data.get(k) ?? null,
    setItemSync: (k: string, v: string) => void data.set(k, v),
  };
}

const tuning: MicTuning = {
  inputId: 'bt-1',
  roomRms: 30,
  voiceRms: 2000,
  lineRms: 245,
  testedAt: '2026-11-12T00:00:00Z',
  automatic: true,
  manualStep: null,
};

describe('createMicTuningStore', () => {
  it('defaults the picked input to the phone and has no tuning', () => {
    const store = createMicTuningStore(fakeStorage());
    expect(store.pickedInput()).toBe('phone');
    expect(store.tuning('phone')).toBeNull();
  });
  it('round-trips through the storage, also for a new store over it', () => {
    const storage = fakeStorage();
    const store = createMicTuningStore(storage);
    store.pick('bt-1');
    store.save(tuning);
    const again = createMicTuningStore(storage);
    expect(again.pickedInput()).toBe('bt-1');
    expect(again.tuning('bt-1')).toEqual(tuning);
    expect(again.tuning('phone')).toBeNull();
  });
  it('writes one key, voice.micTuning, as { picked, tunings }', () => {
    const storage = fakeStorage();
    createMicTuningStore(storage).save(tuning);
    expect(JSON.parse(storage.getItemSync('voice.micTuning') ?? '')).toEqual({
      picked: 'phone',
      tunings: { 'bt-1': tuning },
    });
  });
  it('reads corrupt JSON as empty and never throws', () => {
    for (const raw of ['{nope', 'null', '5', '{"picked":3,"tunings":[]}']) {
      const store = createMicTuningStore(fakeStorage({ 'voice.micTuning': raw }));
      expect(store.pickedInput()).toBe('phone');
      expect(store.tuning('bt-1')).toBeNull();
    }
  });
  it('recovers by saving over corrupt JSON', () => {
    const store = createMicTuningStore(fakeStorage({ 'voice.micTuning': '{nope' }));
    store.save(tuning);
    expect(store.tuning('bt-1')).toEqual(tuning);
  });
  it('notifies subscribers on pick and save, until unsubscribed', () => {
    const store = createMicTuningStore(fakeStorage());
    const cb = vi.fn();
    const off = store.subscribe(cb);
    store.pick('bt-1');
    store.save(tuning);
    expect(cb).toHaveBeenCalledTimes(2);
    off();
    store.pick('phone');
    expect(cb).toHaveBeenCalledTimes(2);
  });
});
