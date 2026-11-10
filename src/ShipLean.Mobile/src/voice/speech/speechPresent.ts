// Whether the PCM holds speech at all, so silence ends as nothing heard without decoding.
export function speechPresent(pcm: Int16Array): boolean {
  void pcm;
  throw new Error('TODO: detect speech in PCM');
}
