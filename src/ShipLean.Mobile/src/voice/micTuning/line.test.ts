import { describe, expect, it } from 'vitest';
import type { MicTuning } from '../contracts';
import { activeLine, classify, lineToSlider, sliderToLine } from './line';

const frames = (level: number, n = 40) => Array.from({ length: n }, () => level);
// A hold where the person speaks with pauses: 10 frames of room, then 30 of voice.
const hold = (room: number, voice: number) => [...frames(room, 10), ...frames(voice, 30)];

describe('classify', () => {
  it('zeros are noSignal', () => {
    expect(classify(frames(0), hold(0, 0)).kind).toBe('noSignal');
  });
  it('a voice barely over the room is noisy', () => {
    expect(classify(frames(400), hold(400, 600)).kind).toBe('noisy');
  });
  it('a quiet voice that only the line hears is tooQuiet', () => {
    expect(classify(frames(30), hold(30, 200)).kind).toBe('tooQuiet');
  });
  it('a loud voice in a quiet room is good', () => {
    expect(classify(frames(30), hold(30, 2000)).kind).toBe('good');
  });
  it('checks noisy before tooQuiet when a hold is under the floor but over its line', () => {
    expect(classify(frames(200), hold(200, 350)).kind).toBe('noisy');
  });
  it('is noisy, not tooQuiet, when the hold is under the floor but over its line', () => {
    // Pauses below the room level keep the noise estimate low, so only the line order decides this case.
    const voice = [...frames(20, 10), ...frames(280, 30)];
    expect(classify(frames(200), voice).kind).toBe('noisy');
  });
  it('lineRms is the geometric mean of room and voice', () => {
    const r = classify(frames(30), hold(30, 2000));
    expect(r.roomRms).toBe(30);
    expect(r.voiceRms).toBe(2000);
    expect(r.lineRms).toBeCloseTo(Math.sqrt(30 * 2000), 6);
  });
  it('clamps lineRms to 20..1500', () => {
    expect(classify(frames(1), hold(1, 5)).lineRms).toBe(20);
    expect(classify(frames(1000), hold(1000, 30000)).lineRms).toBe(1500);
  });
  it('takes the voice level from the frames above the room', () => {
    const voice = [...frames(10, 20), ...frames(900, 20)];
    expect(classify(frames(50), voice).voiceRms).toBe(900);
  });
});

describe('slider', () => {
  it('maps step 1 to 1500 and step 10 to 20', () => {
    expect(sliderToLine(1)).toBeCloseTo(1500, 6);
    expect(sliderToLine(10)).toBeCloseTo(20, 6);
  });
  it('is more sensitive as the step rises', () => {
    expect(sliderToLine(7)).toBeLessThan(sliderToLine(3));
  });
  it('round-trips 1..10', () => {
    for (let step = 1; step <= 10; step++) expect(lineToSlider(sliderToLine(step))).toBe(step);
  });
  it('clamps lines outside the range', () => {
    expect(lineToSlider(5000)).toBe(1);
    expect(lineToSlider(1)).toBe(10);
  });
});

describe('activeLine', () => {
  const base: MicTuning = {
    inputId: 'phone',
    roomRms: 30,
    voiceRms: 2000,
    lineRms: 245,
    testedAt: '2026-11-12T00:00:00Z',
    automatic: true,
    manualStep: null,
  };
  it('is undefined without a tuning', () => {
    expect(activeLine(null)).toBeUndefined();
  });
  it('uses the tested line when automatic', () => {
    expect(activeLine(base)).toBe(245);
  });
  it('uses the slider when manual', () => {
    expect(activeLine({ ...base, automatic: false, manualStep: 10 })).toBeCloseTo(20, 6);
  });
});
