// The speech line maths (pure). Speech is present when at least MIN_SPEECH_FRAMES 30 ms frames are louder than both
// the line (an absolute floor, or the tuned line) and the quietest frames by a margin. The line keeps a quiet room's
// hiss out; the margin keeps steady noise (a fan, traffic) out; the frame count keeps a single tap or click out.
import type { MicTuning } from '../contracts';

export const FRAME = 480; // 30 ms at 16 kHz
export const DEFAULT_FLOOR_RMS = 300; // about -40 dBFS
const ABOVE_NOISE = 4; // 12 dB over the noise level
// The margin never asks for more than this, so a hold that is speech from end to end still counts its louder frames.
const CEILING_RMS = 1500; // about -27 dBFS
const MIN_SPEECH_FRAMES = 5;

export function frameLevels(pcm: Int16Array): number[] {
  const levels: number[] = [];
  for (let start = 0; start + FRAME <= pcm.length; start += FRAME) {
    let sum = 0;
    for (let i = start; i < start + FRAME; i++) sum += pcm[i] * pcm[i];
    levels.push(Math.sqrt(sum / FRAME));
  }
  return levels;
}

// lineRms replaces the default floor; the noise margin and the frame count stay.
export function speechPresentInFrames(levels: number[], lineRms?: number): boolean {
  if (levels.length < MIN_SPEECH_FRAMES) return false;
  const sorted = [...levels].sort((a, b) => a - b);
  // The noise level: the 10th percentile frame, so a hold that is mostly speech still finds its pauses.
  const noise = sorted[Math.floor(sorted.length * 0.1)];
  const threshold = Math.max(
    lineRms ?? DEFAULT_FLOOR_RMS,
    Math.min(CEILING_RMS, noise * ABOVE_NOISE),
  );
  return levels.filter((level) => level > threshold).length >= MIN_SPEECH_FRAMES;
}

export type TuningResultKind = 'good' | 'tooQuiet' | 'noisy' | 'noSignal';

export interface TuningResult {
  kind: TuningResultKind;
  roomRms: number;
  voiceRms: number;
  lineRms: number;
}

function median(values: number[]): number {
  if (values.length === 0) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)];
}

export function classify(roomFrames: number[], voiceFrames: number[]): TuningResult {
  // TODO: the real classification (no signal, too quiet, noisy) and the line halfway between room and voice.
  const roomRms = median(roomFrames);
  const voiceRms = median(voiceFrames);
  return { kind: 'good', roomRms, voiceRms, lineRms: DEFAULT_FLOOR_RMS };
}

export function sliderToLine(_step: number): number {
  // TODO: a log scale over steps 1..10.
  return DEFAULT_FLOOR_RMS;
}

export function lineToSlider(_lineRms: number): number {
  // TODO: the inverse of sliderToLine.
  return 5;
}

// undefined means the default floor.
export function activeLine(t: MicTuning | null): number | undefined {
  if (t === null) return undefined;
  if (t.automatic) return t.lineRms;
  return sliderToLine(t.manualStep ?? 5);
}
