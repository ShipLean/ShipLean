// The held command's PCM, in memory only, up to maxMs. append returns false once it is full.
// Samples are 16 kHz mono int16 (resample's output). Nothing here writes to a file: the PCM lives in this buffer
// until the recognizer has read it and the session drops it. A chunk that would pass maxMs is cut at maxMs, and
// append returns false from the call that fills the buffer on, so the adapter ends the hold there.
const SAMPLES_PER_MS = 16;
const FIRST_CAPACITY = 16000;

export function createPcmBuffer(maxMs: number): {
  append(pcm: Int16Array): boolean;
  pcm(): Int16Array;
  ms(): number;
} {
  const maxSamples = Math.floor(maxMs * SAMPLES_PER_MS);
  let data = new Int16Array(Math.min(FIRST_CAPACITY, maxSamples));
  let length = 0;

  return {
    append(pcm) {
      const room = maxSamples - length;
      const kept = Math.min(room, pcm.length);
      if (length + kept > data.length) {
        // Grow by doubling. Views handed out by pcm() keep the old array, so a decode in flight is unaffected.
        const grown = new Int16Array(
          Math.min(maxSamples, Math.max(length + kept, data.length * 2)),
        );
        grown.set(data.subarray(0, length));
        data = grown;
      }
      data.set(pcm.subarray(0, kept), length);
      length += kept;
      return length < maxSamples;
    },
    pcm() {
      return data.subarray(0, length);
    },
    ms() {
      return length / SAMPLES_PER_MS;
    },
  };
}
