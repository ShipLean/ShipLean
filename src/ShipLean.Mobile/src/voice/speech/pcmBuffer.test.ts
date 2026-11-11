import { describe, expect, it } from 'vitest';
import { createPcmBuffer } from './pcmBuffer';

// 100 ms of 16 kHz PCM, every sample set to value.
const chunk = (value: number) => new Int16Array(1600).fill(value);

describe('pcmBuffer', () => {
  it('stops accepting at 120 000 ms', () => {
    const buffer = createPcmBuffer(120_000);

    for (let i = 1; i < 1200; i++) expect(buffer.append(chunk(i % 100))).toBe(true);
    expect(buffer.ms()).toBe(119_900);
    // The chunk that fills it is kept, and from then on append says it is full and keeps nothing.
    expect(buffer.append(chunk(7))).toBe(false);
    expect(buffer.ms()).toBe(120_000);
    expect(buffer.append(chunk(9))).toBe(false);

    expect(buffer.ms()).toBe(120_000);
    expect(buffer.pcm().length).toBe(1_920_000);
    expect(buffer.pcm().at(-1)).toBe(7);
  });

  it('cuts a chunk that would pass the limit at the limit', () => {
    const buffer = createPcmBuffer(150);

    expect(buffer.append(chunk(1))).toBe(true);
    expect(buffer.append(chunk(2))).toBe(false);

    expect(buffer.ms()).toBe(150);
    expect(buffer.pcm().length).toBe(2400);
  });

  it('keeps every sample in order as it grows', () => {
    const buffer = createPcmBuffer(120_000);
    const seconds = 3;
    for (let i = 0; i < seconds * 10; i++) buffer.append(chunk(i));

    const pcm = buffer.pcm();
    expect(pcm.length).toBe(seconds * 16000);
    for (let i = 0; i < seconds * 10; i++) {
      expect(pcm[i * 1600]).toBe(i);
      expect(pcm[i * 1600 + 1599]).toBe(i);
    }
  });

  it('leaves PCM already handed out unchanged as more is appended', () => {
    const buffer = createPcmBuffer(120_000);
    buffer.append(chunk(5));
    const before = buffer.pcm();

    for (let i = 0; i < 20; i++) buffer.append(chunk(6));

    expect(before.length).toBe(1600);
    expect(before.every((s) => s === 5)).toBe(true);
  });
});
