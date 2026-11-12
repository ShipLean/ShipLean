import { describe, expect, it } from 'vitest';
import type { AudioInput } from '../contracts';
import { resolveInput } from './route';

const phone: AudioInput = { id: 'phone', kind: 'phone', name: 'Phone', connected: true };
const pods: AudioInput = { id: 'bt-1', kind: 'bluetooth', name: 'AirPods Pro', connected: true };

describe('resolveInput', () => {
  it('uses the picked input when it is listed and connected', () => {
    expect(resolveInput('bt-1', [phone, pods])).toEqual(pods);
  });

  it('falls back to the phone when the picked input is disconnected', () => {
    expect(resolveInput('bt-1', [phone, { ...pods, connected: false }])).toEqual(phone);
  });

  it('falls back to the phone when the picked id is unknown', () => {
    expect(resolveInput('usb-9', [phone, pods])).toEqual(phone);
  });
});
