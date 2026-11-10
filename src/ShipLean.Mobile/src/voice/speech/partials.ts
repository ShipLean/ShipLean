// Decodes the PCM so far every everyMs while the button is held, and hands each new text to onText.
export function createPartials(
  decode: (pcm: Int16Array) => Promise<string>,
  opts: { everyMs: number; now(): number; onText(t: string): void },
): { push(pcm: Int16Array): void; stop(): Promise<void> } {
  void decode;
  void opts;
  throw new Error('TODO: partial transcripts while held');
}
