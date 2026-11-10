import { describe, expect, it } from 'vitest';
import type { AppAction, PreviewLine } from '../actions/types';
import { FakeCommandModel, FakeModelStore, FakeRecognizer } from '../test-support/voice';
import type { ModelStatus, ResolveResult, ScreenVoiceSpec, ViewIntent } from './contracts';
import {
  isRejectedLine,
  isYes,
  keptActions,
  keptLineIds,
  PreparedCache,
  restingState,
  startHold,
  turnOf,
} from './machine';

const status = (phase: ModelStatus['phase']): ModelStatus => ({
  ...new FakeModelStore().status(),
  phase,
});

const spec = (screen = 'work-item'): ScreenVoiceSpec => ({
  screen,
  actions: [{ name: 'split', description: 'Split the item', args: {}, webOnly: 'the web' }],
  context: () => ({}),
  hotwords: () => ['SHIP-906'],
  toActions: () => ({ ok: true, actions: [], lines: [], rejected: [], view: null }),
});

const action = (kind: string): AppAction => ({
  area: 'workItem',
  kind,
  itemId: 'item-906',
  key: 'SHIP-906',
});
const line = (id: string, text: string): PreviewLine => ({
  id,
  icon: 'flag',
  text,
  removable: true,
});

const okResult = (
  over: Partial<Extract<ResolveResult, { ok: true }>> = {},
): Extract<ResolveResult, { ok: true }> => ({
  ok: true,
  actions: [action('setState'), action('addNote')],
  lines: [line('state', 'State: New → Active'), line('note', 'Note on SHIP-906')],
  rejected: [],
  view: null,
  ...over,
});

// A clock the test moves by hand, in milliseconds.
function clock() {
  let at = 1_000_000;
  return { now: () => at, advance: (ms: number) => (at += ms) };
}

describe('machine', () => {
  it('a release under 500 ms is tooShort and cancels the session', async () => {
    const recognizer = new FakeRecognizer();
    recognizer.script(['set'], 'set this to Active');
    const c = clock();
    const hold = startHold(recognizer, ['Active'], c.now, () => {});
    c.advance(320);
    const out = await hold.release();

    expect(out).toEqual({
      kind: 'failure',
      failure: { kind: 'tooShort', detail: { heldMs: 320 } },
    });
    expect(recognizer.cancelled).toBe(1);
    expect(recognizer.starts).toEqual([{ hotwords: ['Active'] }]);
  });

  it('a transcript with no words is nothingHeard', async () => {
    const recognizer = new FakeRecognizer();
    recognizer.script([], '   ');
    const c = clock();
    const hold = startHold(recognizer, [], c.now, () => {});
    c.advance(1200);

    expect(await hold.release()).toEqual({
      kind: 'failure',
      failure: { kind: 'nothingHeard', detail: {} },
    });
    expect(recognizer.cancelled).toBe(0);
  });

  it('a hold of 500 ms or more hears the transcript and passes on the partial text', async () => {
    const recognizer = new FakeRecognizer();
    recognizer.script(['set this', 'set this to'], 'set this to Active');
    const partials: string[] = [];
    const c = clock();
    const hold = startHold(recognizer, [], c.now, (p) => partials.push(p));
    c.advance(500);
    const out = await hold.release();

    expect(out.kind === 'heard' && out.transcript.text).toBe('set this to Active');
    expect(partials).toEqual(['set this', 'set this to']);
    expect(recognizer.cancelled).toBe(0);
  });

  it('disabled reason follows skipped, downloading, then micOff precedence', () => {
    expect(restingState(status('skipped'), 'deniedFinal')).toEqual({
      kind: 'disabled',
      reason: 'skipped',
      permission: 'deniedFinal',
    });
    expect(restingState(status('none'), 'deniedFinal')).toMatchObject({ reason: 'noModels' });
    for (const phase of ['downloading', 'paused', 'checking'] as const)
      expect(restingState(status(phase), 'deniedCanAsk')).toMatchObject({
        kind: 'disabled',
        reason: 'downloading',
      });
    expect(restingState(status('ready'), 'deniedCanAsk')).toMatchObject({ reason: 'micOff' });
    expect(restingState(status('ready'), 'deniedFinal')).toMatchObject({ reason: 'micOff' });
    expect(restingState(status('ready'), 'undetermined')).toEqual({
      kind: 'idle',
      permission: 'undetermined',
    });
  });

  it('prepare runs once per spec across three commands', async () => {
    const model = new FakeCommandModel();
    const cache = new PreparedCache();
    const workItem = spec('work-item');
    for (let i = 0; i < 3; i++) await cache.get(model, workItem);
    expect(model.prepared).toEqual(['work-item']);

    await cache.get(model, spec('backlog'));
    expect(model.prepared).toEqual(['work-item', 'backlog']);
  });

  it('prepare is keyed by the screen and its action names, not the spec object', async () => {
    const model = new FakeCommandModel();
    const cache = new PreparedCache();
    // Screens build their spec on every render: the same screen and actions keep the prepared prefix.
    for (let i = 0; i < 3; i++) await cache.get(model, spec('work-item'));
    expect(model.prepared).toEqual(['work-item']);

    const more = spec('work-item');
    await cache.get(model, {
      ...more,
      actions: [...more.actions, { name: 'merge', description: 'Merge', args: {} }],
    });
    expect(model.prepared).toEqual(['work-item', 'work-item']);
  });

  it('a rejected prepare is prepared again on the next get', async () => {
    const model = new FakeCommandModel();
    const prepare = model.prepare.bind(model);
    let fail = true;
    model.prepare = (s) => {
      if (!fail) return prepare(s);
      fail = false;
      return Promise.reject(new Error('The model failed to load.'));
    };
    const cache = new PreparedCache();
    const workItem = spec('work-item');

    await expect(cache.get(model, workItem)).rejects.toThrow('The model failed to load.');
    expect(await cache.get(model, workItem)).toEqual({ screen: 'work-item' });
    expect(model.prepared).toEqual(['work-item']);
  });

  it('isYes takes only "yes" or "apply"', () => {
    expect(isYes('Yes.')).toBe(true);
    expect(isYes('apply')).toBe(true);
    expect(isYes('yes, apply')).toBe(true);
    expect(isYes('yes and set it to Done')).toBe(false);
    expect(isYes('')).toBe(false);
  });

  it('keeps the action of each kept line by lines[i] ↔ actions[i], and refuses unequal lengths', () => {
    const result = okResult();
    expect(keptActions(result, ['note'])).toEqual([action('addNote')]);
    expect(keptActions(result, undefined)).toEqual([action('setState'), action('addNote')]);
    expect(() => keptActions(okResult({ lines: [line('state', 'State')] }), ['state'])).toThrow(
      /1 preview lines for 2 actions/,
    );
  });

  it('a preview strikes out the rejected parts with the reason, and Apply leaves them and removed lines out', () => {
    const shown = turnOf(
      spec(),
      okResult({
        rejected: [
          {
            call: { action: 'split', args: {} },
            failure: { kind: 'webOnly', detail: { action: 'split', where: 'the web' } },
          },
        ],
      }),
      'set this to Active, note it, and split it',
      'voice',
    );
    if (shown.kind !== 'preview') throw new Error(`expected a preview, got ${shown.kind}`);
    const struck = shown.state.lines[2];
    expect(isRejectedLine(struck.id)).toBe(true);
    expect(struck).toMatchObject({
      text: 'Split the item',
      warning: 'Do this on the web.',
      removable: false,
    });
    expect(keptLineIds(shown.state)).toEqual(['state', 'note']);
    expect(keptLineIds({ ...shown.state, removed: ['state'] })).toEqual(['note']);
  });

  it('a command with nothing to change shows its view, or fails with the first rejected reason', () => {
    const answer: ViewIntent = { kind: 'answer', content: { lead: 'Two', rows: [] } };
    expect(
      turnOf(spec(), okResult({ actions: [], lines: [], view: answer }), 'what', 'voice'),
    ).toEqual({
      kind: 'view',
      view: answer,
    });
    const failure = { kind: 'webOnly', detail: { action: 'split', where: 'the web' } } as const;
    expect(
      turnOf(
        spec(),
        okResult({
          actions: [],
          lines: [],
          rejected: [{ call: { action: 'split', args: {} }, failure }],
        }),
        'split it',
        'voice',
      ),
    ).toEqual({ kind: 'failure', failure });
  });
});
