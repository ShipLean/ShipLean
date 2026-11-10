import type { KeyValueStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import { cameBackOn, readLastMic, readLastRoute, writeLastMic, writeLastRoute } from './lastPlace';

function memory(): KeyValueStore {
  const m = new Map<string, string>();
  return {
    getItem: (k) => m.get(k) ?? null,
    setItem: (k, v) => void m.set(k, v),
    removeItem: (k) => void m.delete(k),
  };
}

describe('lastPlace', () => {
  it('keeps the last read permission and the route', () => {
    const kv = memory();
    expect(readLastMic(kv)).toBeNull();
    expect(readLastRoute(kv)).toBeNull();
    writeLastMic(kv, 'deniedFinal');
    writeLastRoute(kv, '/p/SHIP/backlog');
    expect(readLastMic(kv)).toBe('deniedFinal');
    expect(readLastRoute(kv)).toBe('/p/SHIP/backlog');
  });

  it('ignores a stored value that is not a state', () => {
    const kv = memory();
    kv.setItem('voice.mic.lastRead', 'maybe');
    expect(readLastMic(kv)).toBeNull();
  });

  it('is back on only from a state that was not granted, to granted', () => {
    expect(cameBackOn('deniedFinal', 'granted')).toBe(true);
    expect(cameBackOn('deniedCanAsk', 'granted')).toBe(true);
    expect(cameBackOn('undetermined', 'granted')).toBe(true);
    expect(cameBackOn('granted', 'granted')).toBe(false);
    expect(cameBackOn(null, 'granted')).toBe(false);
    expect(cameBackOn('deniedFinal', 'deniedFinal')).toBe(false);
    expect(cameBackOn('granted', 'deniedFinal')).toBe(false);
  });
});
