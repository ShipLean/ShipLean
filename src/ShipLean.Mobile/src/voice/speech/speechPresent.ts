// Whether the PCM holds speech at all, so silence ends as nothing heard without decoding. The energy check is in
// micTuning/line.ts: this is the PCM entry point. lineRms replaces the default floor (the tuned speech line).
import { frameLevels, speechPresentInFrames } from '../micTuning/line';

export function speechPresent(pcm: Int16Array, lineRms?: number): boolean {
  return speechPresentInFrames(frameLevels(pcm), lineRms);
}
