import { describe, expect, it } from 'vitest';
import { speechPresent } from './speechPresent';

const RATE = 16000;

// Deterministic noise at an RMS of about rms (a small LCG, so runs are repeatable).
function noise(seconds: number, rms: number, seed = 1): Float64Array {
  let state = seed;
  const out = new Float64Array(Math.round(seconds * RATE));
  for (let i = 0; i < out.length; i++) {
    state = (state * 1664525 + 1013904223) % 4294967296;
    out[i] = (state / 4294967296 - 0.5) * rms * Math.sqrt(12);
  }
  return out;
}

// A voiced phrase: a 140 Hz voice with its harmonics, opening and closing four times a second like syllables.
function phrase(seconds: number, peak: number): Float64Array {
  const out = new Float64Array(Math.round(seconds * RATE));
  for (let i = 0; i < out.length; i++) {
    const t = i / RATE;
    const syllables = Math.max(0, Math.sin(2 * Math.PI * 4 * t));
    let voice = 0;
    for (let h = 1; h <= 8; h++) voice += Math.sin(2 * Math.PI * 140 * h * t) / h;
    out[i] = (peak / 2) * syllables * voice;
  }
  return out;
}

function pcm(...parts: Float64Array[]): Int16Array {
  const total = parts.reduce((n, p) => n + p.length, 0);
  const out = new Int16Array(total);
  let at = 0;
  for (const part of parts) {
    for (let i = 0; i < part.length; i++)
      out[at + i] = Math.max(-32768, Math.min(32767, Math.round(part[i])));
    at += part.length;
  }
  return out;
}

function mix(a: Float64Array, b: Float64Array): Float64Array {
  return a.map((v, i) => v + (b[i] ?? 0));
}

describe('speechPresent', () => {
  it('silence gives false and a spoken phrase gives true', () => {
    const quietRoom = noise(2, 30);
    expect(speechPresent(pcm(new Float64Array(2 * RATE)))).toBe(false);
    expect(speechPresent(pcm(quietRoom))).toBe(false);

    // Half a second of room, a 1.5 s phrase at a normal speaking level (about -24 dBFS), then room again.
    const spoken = pcm(
      noise(0.5, 30, 2),
      mix(phrase(1.5, 4000), noise(1.5, 30, 3)),
      noise(0.5, 30, 4),
    );
    expect(speechPresent(spoken)).toBe(true);
  });

  it('a voice far off, under the floor, gives false', () => {
    // A television in the next room: speech-shaped, but about -50 dBFS at the phone.
    expect(
      speechPresent(pcm(new Float64Array(8000), phrase(1.5, 200), new Float64Array(8000))),
    ).toBe(false);
  });

  it('a tuned line below the default floor lets a quiet voice through', () => {
    // A voice at an RMS of 150 (a sine of peak 150 * sqrt(2)): under the default floor of 300, over a line of 100.
    const quietVoice = new Float64Array(RATE).map(
      (_, i) => 150 * Math.SQRT2 * Math.sin((2 * Math.PI * 200 * i) / RATE),
    );
    const held = pcm(new Float64Array(8000), quietVoice, new Float64Array(8000));

    expect(speechPresent(held)).toBe(false);
    expect(speechPresent(held, 100)).toBe(true);
  });

  it('a phrase with no pause around it still counts', () => {
    expect(speechPresent(pcm(phrase(0.8, 4000)))).toBe(true);
  });

  it('steady loud noise, a single tap and a hold too short to say anything give false', () => {
    expect(speechPresent(pcm(noise(2, 900)))).toBe(false);

    const tap = noise(0.06, 12000, 5);
    expect(speechPresent(pcm(noise(1, 30), tap, noise(1, 30, 6)))).toBe(false);

    expect(speechPresent(pcm(phrase(0.1, 4000)))).toBe(false);
  });

  it('a quiet phrase over a fan is heard', () => {
    const fan = noise(2.5, 200, 7);
    const words = new Float64Array(2.5 * RATE);
    words.set(phrase(1.5, 3000), 0.5 * RATE);
    expect(speechPresent(pcm(mix(fan, words)))).toBe(true);
  });
});
