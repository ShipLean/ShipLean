// The held command's PCM, in memory only, up to maxMs. append returns false once it is full.
export function createPcmBuffer(maxMs: number): {
  append(pcm: Int16Array): boolean;
  pcm(): Int16Array;
  ms(): number;
} {
  void maxMs;
  throw new Error('TODO: the in-memory PCM buffer');
}
