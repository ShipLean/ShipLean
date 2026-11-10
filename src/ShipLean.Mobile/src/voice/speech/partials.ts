// Decodes the PCM so far every everyMs while the button is held, and hands each new text to onText.
// Parakeet decodes offline only, so a partial is a re-decode of everything held so far. push takes that whole PCM
// (the buffer's pcm()), not a chunk. Rules:
// - the first decode starts once everyMs has passed since the first push, and each later one at least everyMs
//   after the one before started;
// - never two decodes at once: PCM pushed while a decode runs replaces any earlier waiting PCM, so only the latest
//   is decoded next (on the first push once the wait is over, or when the running decode ends late);
// - onText gets a text only when it differs from the last one, and nothing after stop();
// - stop() resolves once no decode is running, so the final decode never overlaps a partial. A failed partial
//   decode ends the partials and stop() rejects with its error.
export function createPartials(
  decode: (pcm: Int16Array) => Promise<string>,
  opts: { everyMs: number; now(): number; onText(t: string): void },
): { push(pcm: Int16Array): void; stop(): Promise<void> } {
  let waiting: Int16Array | null = null;
  let running: Promise<void> | null = null;
  let firstPushAt: number | null = null;
  let lastStartAt: number | null = null;
  let lastText: string | null = null;
  let stopped = false;
  let failure: { error: unknown } | null = null;

  function due(): boolean {
    const since = lastStartAt ?? firstPushAt;
    return since !== null && opts.now() - since >= opts.everyMs;
  }

  function startIfDue(): void {
    if (stopped || failure || running || !waiting || !due()) return;
    const pcm = waiting;
    waiting = null;
    lastStartAt = opts.now();
    running = decode(pcm).then(
      (text) => {
        running = null;
        if (stopped) return;
        if (text !== lastText) {
          lastText = text;
          opts.onText(text);
        }
        startIfDue();
      },
      (error: unknown) => {
        running = null;
        failure = { error };
      },
    );
  }

  return {
    push(pcm) {
      if (stopped) return;
      firstPushAt ??= opts.now();
      waiting = pcm;
      startIfDue();
    },
    async stop() {
      stopped = true;
      waiting = null;
      while (running) await running;
      if (failure) throw failure.error;
    },
  };
}
