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

const MIN_LINE_RMS = 20;
const MAX_LINE_RMS = CEILING_RMS;
const SILENT_RMS = 2;
const MIN_VOICE_OVER_ROOM = 2;
const MIN_STEP = 1;
const MAX_STEP = 10;

function percentile90(values: number[]): number {
  if (values.length === 0) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * 0.9))];
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

// The room level is the loud end of the quiet step; the voice level is the loud end of the frames of the speaking step
// that rise above the room (the whole step when none do). The line sits halfway between them on a log scale.
export function classify(roomFrames: number[], voiceFrames: number[]): TuningResult {
  const roomRms = percentile90(roomFrames);
  const aboveRoom = voiceFrames.filter((level) => level > roomRms);
  const voiceRms = percentile90(aboveRoom.length > 0 ? aboveRoom : voiceFrames);
  const lineRms = clamp(Math.sqrt(roomRms * voiceRms), MIN_LINE_RMS, MAX_LINE_RMS);
  const result = (kind: TuningResultKind): TuningResult => ({ kind, roomRms, voiceRms, lineRms });

  if ([...roomFrames, ...voiceFrames].every((level) => level < SILENT_RMS))
    return result('noSignal');
  if (voiceRms / roomRms < MIN_VOICE_OVER_ROOM) return result('noisy');
  if (!speechPresentInFrames(voiceFrames) && speechPresentInFrames(voiceFrames, lineRms)) {
    return result('tooQuiet');
  }
  return result('good');
}

// Step 1 is the least sensitive (the highest line), step 10 the most sensitive (the lowest), on a log scale.
export function sliderToLine(step: number): number {
  const t = (clamp(step, MIN_STEP, MAX_STEP) - MIN_STEP) / (MAX_STEP - MIN_STEP);
  return MAX_LINE_RMS * Math.pow(MIN_LINE_RMS / MAX_LINE_RMS, t);
}

export function lineToSlider(lineRms: number): number {
  const t =
    Math.log(MAX_LINE_RMS / clamp(lineRms, MIN_LINE_RMS, MAX_LINE_RMS)) /
    Math.log(MAX_LINE_RMS / MIN_LINE_RMS);
  return clamp(Math.round(MIN_STEP + t * (MAX_STEP - MIN_STEP)), MIN_STEP, MAX_STEP);
}

// undefined means the default floor.
export function activeLine(t: MicTuning | null): number | undefined {
  if (t === null) return undefined;
  if (t.automatic) return t.lineRms;
  return sliderToLine(t.manualStep ?? 5);
}
