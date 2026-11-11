// Whether the PCM holds speech at all, so silence ends as nothing heard without decoding.
// An energy check in pure code over 16 kHz mono int16: the PCM is cut into 30 ms frames, and speech is present when
// at least MIN_SPEECH_FRAMES of them (150 ms in all) are louder than both an absolute floor and the quietest frames
// by a margin. The absolute floor keeps a quiet room's hiss out; the margin keeps steady noise (a fan, traffic) out;
// the frame count keeps a single tap or click out.
const FRAME = 480;
const FLOOR_RMS = 300; // about -40 dBFS
const ABOVE_NOISE = 4; // 12 dB over the noise level
// The margin never asks for more than this, so a hold that is speech from end to end (no pause to measure the
// noise in) still counts its louder frames.
const CEILING_RMS = 1500; // about -27 dBFS
const MIN_SPEECH_FRAMES = 5;

export function speechPresent(pcm: Int16Array): boolean {
  const levels: number[] = [];
  for (let start = 0; start + FRAME <= pcm.length; start += FRAME) {
    let sum = 0;
    for (let i = start; i < start + FRAME; i++) sum += pcm[i] * pcm[i];
    levels.push(Math.sqrt(sum / FRAME));
  }
  if (levels.length < MIN_SPEECH_FRAMES) return false;
  const sorted = [...levels].sort((a, b) => a - b);
  // The noise level: the 10th percentile frame, so a hold that is mostly speech still finds its pauses.
  const noise = sorted[Math.floor(sorted.length * 0.1)];
  const threshold = Math.max(FLOOR_RMS, Math.min(CEILING_RMS, noise * ABOVE_NOISE));
  return levels.filter((level) => level > threshold).length >= MIN_SPEECH_FRAMES;
}
