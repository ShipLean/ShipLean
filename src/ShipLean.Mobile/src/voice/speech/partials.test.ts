import { describe, expect, it } from 'vitest';
import { createPartials } from './partials';

// A decode the test resolves by hand, recording which PCM each call got and how many ran at once.
function controlledDecode() {
  const calls: { pcm: Int16Array; resolve(text: string): void; reject(e: Error): void }[] = [];
  let running = 0;
  let mostAtOnce = 0;
  const decode = (pcm: Int16Array) =>
    new Promise<string>((resolve, reject) => {
      running++;
      mostAtOnce = Math.max(mostAtOnce, running);
      const settle = () => {
        running--;
      };
      calls.push({
        pcm,
        resolve: (t) => {
          settle();
          resolve(t);
        },
        reject: (e) => {
          settle();
          reject(e);
        },
      });
    });
  return { decode, calls, mostAtOnce: () => mostAtOnce };
}

function setup() {
  let clock = 0;
  const texts: string[] = [];
  const d = controlledDecode();
  const partials = createPartials(d.decode, {
    everyMs: 700,
    now: () => clock,
    onText: (t) => texts.push(t),
  });
  return { partials, texts, ...d, at: (ms: number) => (clock = ms) };
}

// The buffer so far, tagged so a test can tell which push a decode got.
const pcm = (tag: number) => Int16Array.from([tag]);
const settled = () => new Promise((r) => setTimeout(r, 0));

describe('partials', () => {
  it('never runs two decodes at once and emits only the latest text', async () => {
    const { partials, texts, calls, mostAtOnce, at } = setup();
    at(0);
    partials.push(pcm(1));
    at(700);
    partials.push(pcm(2));
    expect(calls).toHaveLength(1);

    // While the first decode runs, more audio arrives: none starts a decode, and only the latest is kept.
    at(1400);
    partials.push(pcm(3));
    at(1500);
    partials.push(pcm(4));
    expect(calls).toHaveLength(1);

    calls[0].resolve('set ship');
    await settled();
    // The first decode ended late, so the latest PCM is decoded at once; 3 is skipped.
    expect(calls.map((c) => c.pcm[0])).toEqual([2, 4]);
    expect(mostAtOnce()).toBe(1);

    calls[1].resolve('set ship 906');
    await settled();
    expect(texts).toEqual(['set ship', 'set ship 906']);
  });

  it('waits at least 700 ms between decodes', async () => {
    const { partials, calls, at } = setup();
    at(0);
    partials.push(pcm(1));
    at(699);
    partials.push(pcm(2));
    expect(calls).toHaveLength(0);
    at(700);
    partials.push(pcm(3));
    expect(calls).toHaveLength(1);

    calls[0].resolve('set');
    await settled();
    at(1000);
    partials.push(pcm(4));
    at(1399);
    partials.push(pcm(5));
    expect(calls).toHaveLength(1);
    at(1400);
    partials.push(pcm(6));

    expect(calls.map((c) => c.pcm[0])).toEqual([3, 6]);
  });

  it('emits a text only when it changed, and nothing after stop', async () => {
    const { partials, texts, calls, at } = setup();
    at(0);
    partials.push(pcm(1));
    at(700);
    partials.push(pcm(1));
    calls[0].resolve('done');
    await settled();
    at(1400);
    partials.push(pcm(2));
    calls[1].resolve('done');
    await settled();
    at(2100);
    partials.push(pcm(3));

    const stopped = partials.stop();
    calls[2].resolve('done now');
    await stopped;
    partials.push(pcm(4));

    expect(texts).toEqual(['done']);
    expect(calls).toHaveLength(3);
  });

  it('stop waits for the running decode, so the final decode never overlaps it', async () => {
    const { partials, calls, at } = setup();
    at(0);
    partials.push(pcm(1));
    at(700);
    partials.push(pcm(2));
    let done = false;

    const stopped = partials.stop().then(() => (done = true));
    await settled();
    expect(done).toBe(false);
    calls[0].resolve('x');
    await stopped;

    expect(done).toBe(true);
  });

  it('stops after a failed decode and stop rejects with its error', async () => {
    const { partials, calls, at } = setup();
    at(0);
    partials.push(pcm(1));
    at(700);
    partials.push(pcm(2));
    calls[0].reject(new Error('decoder gone'));
    await settled();
    at(1400);
    partials.push(pcm(3));

    expect(calls).toHaveLength(1);
    await expect(partials.stop()).rejects.toThrow('decoder gone');
  });
});
