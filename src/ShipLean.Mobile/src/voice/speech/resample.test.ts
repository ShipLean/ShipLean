import { describe, expect, it } from 'vitest';
import { resample } from './resample';

// A sine at hz, interleaved over channels (the same on each), as float32 -1..1.
function sineFloat(
  hz: number,
  rate: number,
  seconds: number,
  channels: number,
  amp = 0.5,
): Float32Array {
  const frames = Math.round(rate * seconds);
  const out = new Float32Array(frames * channels);
  for (let f = 0; f < frames; f++) {
    for (let c = 0; c < channels; c++)
      out[f * channels + c] = amp * Math.sin((2 * Math.PI * hz * f) / rate);
  }
  return out;
}

describe('resample', () => {
  it('48 kHz float32 stereo becomes 16 kHz mono int16 of the right length', () => {
    const input = sineFloat(440, 48000, 0.5, 2);

    const out = resample(input, 48000, 2);

    expect(out).toBeInstanceOf(Int16Array);
    expect(out.length).toBe(8000);
    // Sample for sample it is still the 440 Hz sine at 16 kHz, scaled to int16 (the box filter loses under 1%).
    for (const i of [1, 9, 18, 500, 7999]) {
      const expected = 0.5 * 32767 * Math.sin((2 * Math.PI * 440 * (i * 3 + 1)) / 48000);
      expect(Math.abs(out[i] - expected)).toBeLessThan(200);
    }
  });

  it('16 kHz int16 passes through unchanged', () => {
    const input = Int16Array.from([0, 1, -1, 32767, -32768, 1234, -4321]);

    const out = resample(input, 16000, 1);

    expect(Array.from(out)).toEqual(Array.from(input));
  });

  it('averages the channels to mono', () => {
    const input = Int16Array.from([1000, 3000, -2000, 0]);

    expect(Array.from(resample(input, 16000, 2))).toEqual([2000, -1000]);
  });

  it('takes any delivered rate, up or down', () => {
    expect(resample(new Int16Array(44100), 44100, 1).length).toBe(16000);
    expect(resample(new Int16Array(8000), 8000, 1).length).toBe(16000);
    const up = resample(Int16Array.from([0, 100, 200]), 8000, 1);
    expect(Array.from(up.slice(0, 5))).toEqual([0, 50, 100, 150, 200]);
  });

  it('clamps float32 outside -1..1 to the int16 range', () => {
    expect(Array.from(resample(Float32Array.from([1.5, -1.5]), 16000, 1))).toEqual([32767, -32768]);
  });
});
