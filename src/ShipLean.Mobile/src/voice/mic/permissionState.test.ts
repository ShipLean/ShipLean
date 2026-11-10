import { describe, expect, it } from 'vitest';
import { permissionState } from './permissionState';

describe('permissionState', () => {
  it('maps granted, undetermined, denied with canAskAgain, and denied without it to the four states', () => {
    expect(permissionState({ granted: true, status: 'granted', canAskAgain: true })).toBe(
      'granted',
    );
    expect(permissionState({ granted: false, status: 'undetermined', canAskAgain: true })).toBe(
      'undetermined',
    );
    expect(permissionState({ granted: false, status: 'denied', canAskAgain: true })).toBe(
      'deniedCanAsk',
    );
    expect(permissionState({ granted: false, status: 'denied', canAskAgain: false })).toBe(
      'deniedFinal',
    );
  });
});
