import { describe, expect, it } from 'vitest';
import type { ParsedCall } from '../../src/voice/contracts';
import { renderReport, scoreRuns, type RunLine } from './score';

const COMMENT = 'comment on SHIP-12 the build is green again';
const NOTE = 'the build is green again';
const NOTE_SPAN = { start: 19, end: 43 };

function line(over: Partial<RunLine> & { output: string }): RunLine {
  return {
    row: 0,
    screen: 'fixture',
    utterance: COMMENT,
    tier: 'text',
    model: 'qwen3-1.7b',
    transcript: over.utterance ?? COMMENT,
    llmMs: 100,
    sttMs: null,
    expectedCalls: [{ action: 'comment', args: { key: 'SHIP-12' }, noteSpan: NOTE_SPAN }],
    expectedNote: NOTE,
    cpu: 'Test CPU',
    threads: 4,
    ...over,
  };
}

const out = (calls: ParsedCall[]) => JSON.stringify(calls);

describe('score', () => {
  it('counts right action, right args and exact note per action, and flags actions under 90 %', () => {
    const setState = 'move SHIP-40 to done';
    const setStateCall = { action: 'setState', args: { key: 'SHIP-40', state: 'Done' } };
    const lines = [
      // All right.
      line({
        row: 0,
        llmMs: 100,
        output: out([{ action: 'comment', args: { key: 'SHIP-12' }, noteSpan: NOTE_SPAN }]),
      }),
      // Right action, wrong key, the note still exact.
      line({
        row: 1,
        llmMs: 300,
        output: out([{ action: 'comment', args: { key: 'SHIP-40' }, noteSpan: NOTE_SPAN }]),
      }),
      // Wrong action: no note either.
      line({ row: 2, llmMs: 200, output: out([setStateCall]) }),
      // Text the phone can't parse is no call at all.
      line({ row: 3, llmMs: 400, output: '[{"action": "comment", "args": {"key": "SHIP-12"}' }),
      line({
        row: 4,
        utterance: setState,
        expectedCalls: [setStateCall],
        expectedNote: null,
        llmMs: 50,
        output: out([setStateCall]),
      }),
      line({
        row: 5,
        utterance: "what's the weather like",
        expectedCalls: [],
        expectedNote: null,
        llmMs: 70,
        output: '[]',
      }),
    ];

    const [run] = scoreRuns(lines);

    expect(run).toMatchObject({ tier: 'text', model: 'qwen3-1.7b', screen: 'fixture', rows: 6 });
    expect(run.actions).toEqual([
      {
        action: '(none)',
        rows: 1,
        rightAction: 1,
        rightArgs: 1,
        noteRows: 0,
        exactNote: 0,
        medianMs: 70,
        p90Ms: 70,
        flagged: false,
      },
      {
        action: 'comment',
        rows: 4,
        rightAction: 2,
        rightArgs: 1,
        noteRows: 4,
        exactNote: 2,
        medianMs: 200,
        p90Ms: 400,
        flagged: true,
      },
      {
        action: 'setState',
        rows: 1,
        rightAction: 1,
        rightArgs: 1,
        noteRows: 0,
        exactNote: 0,
        medianMs: 50,
        p90Ms: 50,
        flagged: false,
      },
    ]);

    // Right action and args with the note off is still flagged.
    const noteOff = line({
      model: 'qwen3-4b',
      output: out([
        { action: 'comment', args: { key: 'SHIP-12' }, noteSpan: { start: 11, end: 18 } },
      ]),
    });
    expect(scoreRuns([noteOff])[0].actions[0]).toMatchObject({
      rightAction: 1,
      rightArgs: 1,
      exactNote: 0,
      flagged: true,
    });

    const report = renderReport('2026-11-10', lines);
    expect(report).toContain('Host CPU: Test CPU, 4 threads.');
    expect(report).toContain('not phone times');
    expect(report).toContain('| comment | 4 | 50 % | 25 % | 50 % of 4 | 200 | 400 | under 90 % |');
    expect(report).toContain('| setState | 1 | 100 % | 100 % | – | 50 | 50 |  |');
  });

  it("scores the note from the transcript slice, not the model's text", () => {
    // The model writes the note itself and points its span at other words: the phone posts the span, so it's wrong.
    const ownText = line({
      output: out([
        { action: 'comment', args: { key: 'SHIP-12', text: NOTE }, noteSpan: { start: 0, end: 7 } },
      ]),
    });
    // Parakeet heard "ship twelve": the span is cut from that transcript, not from the utterance.
    const heard = 'comment on ship twelve the build is green again';
    const fromTranscript = line({
      tier: 'audio',
      transcript: heard,
      sttMs: 900,
      output: out([
        { action: 'comment', args: { key: 'SHIP-12' }, noteSpan: { start: 23, end: heard.length } },
      ]),
    });
    // A span off the transcript's words gives no note at all.
    const cutWord = line({
      tier: 'audio',
      transcript: heard,
      sttMs: 900,
      output: out([
        {
          action: 'comment',
          args: { key: 'SHIP-12', text: NOTE },
          noteSpan: { start: 25, end: heard.length },
        },
      ]),
    });

    const [text, audio] = scoreRuns([ownText, fromTranscript, cutWord]);

    expect(text.actions[0]).toMatchObject({ noteRows: 1, exactNote: 0 });
    expect(audio.actions[0]).toMatchObject({ noteRows: 2, exactNote: 1, medianMs: 1000 });
  });
});
