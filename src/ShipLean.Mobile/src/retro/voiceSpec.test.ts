import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { MemberDto } from '@shiplean/core/api/projects';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import members from '../../../ShipLean.TestSupport/DtoFixtures/projects/members.json';
import { designInsights, designRetro } from '../test-support/retro';
import { aTranscript } from '../test-support/voice';
import type { ParsedCall, ResolveResult, TestSetRow, VoiceFailure } from '../voice/contracts';
import { resolveCommand } from '../voice/resolve';
import type { RetroAction } from './actions';
import { RETRO_ACTIONS, retroVoiceSpec, type RetroVoiceContext } from './voiceSpec';
import { matchPoint } from './voiceMatch';

const retro = designRetro();
const POINTS: RetroPointDto[] = retro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
const byText = (fragment: string): RetroPointDto => {
  const p = POINTS.find((x) => x.text.includes(fragment));
  if (p === undefined) throw new Error(`no point with ${fragment}`);
  return p;
};
const PROPOSAL = designInsights().mergeProposals[0];
const ACTIONS = [
  {
    workItemId: 'a0000000-0000-4000-8000-000000000958',
    key: 'SHIP-958',
    title: 'Baselines from CI',
  },
  { workItemId: 'a0000000-0000-4000-8000-000000000960', key: 'SHIP-960', title: 'Profile first' },
];

function context(patch: Partial<RetroVoiceContext> = {}): RetroVoiceContext {
  return {
    sprint: { id: retro.sprint.id, name: retro.sprint.name },
    category: null,
    points: POINTS,
    lastTouchedId: null,
    openActions: ACTIONS,
    proposals: [PROPOSAL],
    members: members as MemberDto[],
    projectId: '00000000-0000-4000-8000-0000000000aa',
    projectKey: 'SHIP',
    ...patch,
  };
}

function run(
  ctx: RetroVoiceContext,
  utterance: string,
  calls: [string, Record<string, unknown>][],
  note?: string,
): ResolveResult<RetroAction> {
  const at = note === undefined ? -1 : utterance.indexOf(note);
  const parsed: ParsedCall[] = calls.map(([action, args]) => ({
    action,
    args,
    ...(at >= 0 && RETRO_ACTIONS.find((a) => a.name === action)?.noteArg !== undefined
      ? { noteSpan: { start: at, end: at + (note?.length ?? 0) } }
      : {}),
  }));
  return resolveCommand(retroVoiceSpec(ctx), parsed, aTranscript({ text: utterance }));
}

function done(result: ResolveResult<RetroAction>) {
  if (!result.ok) throw new Error(`failed: ${result.failure.kind}`);
  return result;
}
const failed = (result: ResolveResult<RetroAction>): VoiceFailure => {
  if (result.ok) throw new Error('expected a failure');
  return result.failure;
};
const only = (result: ResolveResult<RetroAction>) => {
  const r = done(result);
  expect(r.actions).toHaveLength(1);
  expect(r.lines).toHaveLength(1);
  return { action: r.actions[0], line: r.lines[0], view: r.view };
};

describe('retro voice spec', () => {
  it('declares nine actions plus two web-only ones and no read it out', () => {
    const spec = retroVoiceSpec(context());
    expect(spec.actions.filter((a) => a.webOnly === undefined).map((a) => a.name)).toEqual([
      'add_point',
      'support',
      'make_action',
      'add_to_action',
      'not_needed',
      'answer_merge',
      'show',
      'ask',
      'add_point_in',
    ]);
    expect(spec.actions.filter((a) => a.webOnly !== undefined).map((a) => a.name)).toEqual([
      'rename_theme',
      'archive_point',
    ]);
    expect(spec.actions.some((a) => /read|speak|aloud/i.test(a.name))).toBe(false);
    for (const a of spec.actions) expect(a.description.length).toBeLessThanOrEqual(110);
  });

  it('add_point drops the command words and keeps the text word for word', () => {
    const { action } = only(
      run(
        context(),
        'Retro note: The S22 build took 40 minutes, again!',
        [['add_point', { note: 'x', category: 'wentBadly' }]],
        'The S22 build took 40 minutes, again!',
      ),
    );
    expect(action).toMatchObject({
      kind: 'addPoint',
      text: 'The S22 build took 40 minutes, again!',
    });
  });

  it('resolves the dictate-preview state with the guessed category marked', () => {
    const note =
      'we should profile the small model on the S22 before SHIP-906 gets more voice actions';
    const { action, line } = only(
      run(
        context(),
        `Retro note: ${note}`,
        [['add_point', { note: 'x', category: 'improve' }]],
        note,
      ),
    );
    expect(action).toMatchObject({ categoryGuessed: true, category: 'improve', key: 'SHIP-906' });
    expect(line.text).toBe(`Can be improved: "${note}" · about SHIP-906`);
    expect(line.detail).toBe('Category picked for you');
    expect(line.removable).toBe(true);
  });

  it('add_point refuses a point with no start phrase before it', () => {
    const f = failed(
      run(
        context(),
        'the model download stalled',
        [['add_point', { note: 'x', category: 'wentBadly' }]],
        'the model download stalled',
      ),
    );
    expect(f.kind).toBe('unclear');
    const filler = failed(
      run(
        context(),
        'Um, the model download stalled',
        [['add_point', { note: 'x', category: 'wentBadly' }]],
        'the model download stalled',
      ),
    );
    expect(filler).toEqual({
      kind: 'unclear',
      detail: { heard: 'Um, the model download stalled' },
    });
    const spoken = only(
      run(
        context(),
        'Retro note colon the model download stalled',
        [['add_point', { note: 'x', category: 'wentBadly' }]],
        'the model download stalled',
      ),
    );
    expect(spoken.action).toMatchObject({ text: 'the model download stalled' });
  });

  it('add_point_in takes the said category and marks nothing as guessed', () => {
    const { action, line } = only(
      run(
        context(),
        'Went badly: two agents edited the same file again',
        [['add_point_in', { category: 'wentBadly', note: 'x' }]],
        'two agents edited the same file again',
      ),
    );
    expect(action).toMatchObject({
      kind: 'addPoint',
      category: 'wentBadly',
      categoryGuessed: false,
    });
    expect(line.detail).toBeUndefined();
  });

  it("keeps a named key as the point's key and never swaps in the sprint", () => {
    const { action } = only(
      run(
        context(),
        'Retro note: we should profile SHIP-958 first',
        [['add_point', { note: 'x', category: 'improve' }]],
        'we should profile SHIP-958 first',
      ),
    );
    expect(action).toMatchObject({ key: 'SHIP-958', itemId: null, sprintId: null });
    const unknown = only(
      run(
        context(),
        'Retro note: SHIP-99999 never existed',
        [['add_point', { note: 'x', category: 'improve' }]],
        'SHIP-99999 never existed',
      ),
    );
    expect(unknown.action).toMatchObject({ key: 'SHIP-99999', sprintId: null });
    const none = only(
      run(
        context(),
        'Retro note: the queue grew',
        [['add_point', { note: 'x', category: 'improve' }]],
        'the queue grew',
      ),
    );
    expect(none.action).toMatchObject({ key: null, sprintId: retro.sprint.id });
  });

  it('refuses a point about no item when no sprint is known, so no unsendable preview is shown', () => {
    const failure = failed(
      run(
        context({ sprint: null }),
        'Retro note: the queue grew',
        [['add_point', { note: 'x', category: 'improve' }]],
        'the queue grew',
      ),
    );
    expect(failure).toEqual({
      kind: 'noValidAction',
      detail: { action: 'add_point', reason: 'invalidArgs' },
    });
    // A point that names an item is still sendable without a sprint.
    const named = only(
      run(
        context({ sprint: null }),
        'Retro note: profile SHIP-958 first',
        [['add_point', { note: 'x', category: 'improve' }]],
        'profile SHIP-958 first',
      ),
    );
    expect(named.action).toMatchObject({ key: 'SHIP-958', sprintId: null });
  });

  it('support gives a plus one on the named point', () => {
    const { action, line } = only(
      run(context(), 'Plus one on the build point', [['support', { point: 'the build point' }]]),
    );
    expect(action).toMatchObject({ kind: 'support', pointId: byText('S22 build').id });
    expect(line.text).toBe('+1 on "The S22 build took 40 minutes."');
    expect(line.removable).toBe(true);
  });

  it('resolves the agree state: plus one that targets the last touched point', () => {
    const touched = byText('Reviews came back');
    const { action } = only(
      run(context({ lastTouchedId: touched.id }), 'Plus one that', [
        ['support', { point: 'that' }],
      ]),
    );
    expect(action).toMatchObject({ kind: 'support', pointId: touched.id });
    expect(touched.id).not.toBe(POINTS[0].id);
  });

  it('asks which point when nothing was touched', () => {
    const f = failed(run(context(), 'Plus one that', [['support', { point: 'that' }]]));
    expect(f.kind).toBe('ambiguous');
    if (f.kind !== 'ambiguous') return;
    const undecided = POINTS.filter((p) => p.decision === 'undecided');
    expect(f.detail.candidates).toEqual(
      undecided.map((p) => ({ key: p.workItem?.key ?? '', title: p.text })),
    );
    expect(f.detail.call.action).toBe('support');
  });

  it('make_action on that titles the action with the touched point text and no owner', () => {
    const touched = byText('Two agents');
    const { action } = only(
      run(context({ lastTouchedId: touched.id }), 'Make that an action', [
        ['make_action', { point: 'that' }],
      ]),
    );
    expect(action).toMatchObject({ kind: 'newAction', title: touched.text, ownerId: null });
  });

  it('resolves the action-preview state: an action for Priya titled with the point text', () => {
    const p = byText('signing key');
    const { action, line } = only(
      run(context(), 'Make an action for Priya from the signing key point', [
        ['make_action', { point: 'the signing key point', person: 'Priya' }],
      ]),
    );
    expect(action).toMatchObject({
      kind: 'newAction',
      title: p.text,
      ownerName: 'Priya Shah',
      pointIds: [p.id],
      sprintId: retro.sprint.id,
    });
    expect(line.text).toBe('New action item "The signing key expired mid-sprint." · Priya Shah');
  });

  it('make_action cuts a long title at 80 characters and hands back an unknown person', () => {
    const long = { ...POINTS[2], text: 'word '.repeat(30).trim() };
    const { action } = only(
      run(context({ points: [long] }), 'Make that an action', [['make_action', { point: 'word' }]]),
    );
    expect(action).toMatchObject({ title: long.text.slice(0, 80) });
    const f = failed(
      run(context(), 'Make an action for Zed from the signing key point', [
        ['make_action', { point: 'signing key', person: 'Zed' }],
      ]),
    );
    expect(f.kind).toBe('noMatch');
  });

  it('add_to_action adds the point to an open action by key', () => {
    const p = byText('signing key');
    const { action } = only(
      run(context(), 'Add the signing key point to SHIP-958', [
        ['add_to_action', { point: 'signing key', action: 'SHIP-958' }],
      ]),
    );
    expect(action).toMatchObject({
      kind: 'addToAction',
      key: 'SHIP-958',
      itemId: ACTIONS[0].workItemId,
      pointIds: [p.id],
    });
    const f = failed(
      run(context(), 'Add the signing key point to SHIP-777', [
        ['add_to_action', { point: 'signing key', action: 'SHIP-777' }],
      ]),
    );
    expect(f.kind).toBe('noMatch');
  });

  it('not_needed keeps the reason word for word', () => {
    const { action, line } = only(
      run(context(), 'The signing key point is not needed, SHIP-958 covers it', [
        ['not_needed', { point: 'signing key', reason: 'ship-958 covers it' }],
      ]),
    );
    expect(action).toMatchObject({ kind: 'notNeeded', reason: 'SHIP-958 covers it' });
    expect(line.text).toBe('Not needed · "SHIP-958 covers it"');
  });

  it('not_needed accepts no reason', () => {
    const { action, line } = only(
      run(context(), 'Mark the signing key point not needed', [
        ['not_needed', { point: 'signing key' }],
      ]),
    );
    expect(action).toMatchObject({ kind: 'notNeeded', reason: '' });
    expect(line.text).toBe('Not needed');
  });

  it('refuses a decision on a Went well point', () => {
    for (const [name, args] of [
      ['make_action', { point: 'contracts' }],
      ['add_to_action', { point: 'contracts', action: 'SHIP-958' }],
      ['not_needed', { point: 'contracts' }],
    ] as [string, Record<string, unknown>][]) {
      const f = failed(run(context(), 'On contracts', [[name, args]]));
      expect(f, name).toEqual({
        kind: 'noValidAction',
        detail: { action: name, reason: 'notAllowed' },
      });
    }
  });

  it('answer_merge with no pending proposal is not allowed', () => {
    const f = failed(
      run(context({ proposals: [] }), 'Merge them', [['answer_merge', { answer: 'merge' }]]),
    );
    expect(f).toEqual({
      kind: 'noValidAction',
      detail: { action: 'answer_merge', reason: 'notAllowed' },
    });
  });

  it('answer_merge needs no proposal when one is pending and asks when several are', () => {
    const merge = only(run(context(), 'Merge them', [['answer_merge', { answer: 'merge' }]]));
    expect(merge.action).toMatchObject({
      kind: 'mergeThemes',
      projectId: context().projectId,
      proposalId: PROPOSAL.id,
      fromName: PROPOSAL.from.name,
      intoName: PROPOSAL.into.name,
    });
    const apart = only(
      run(context(), 'Keep them apart', [['answer_merge', { answer: 'keepApart' }]]),
    );
    expect(apart.action.kind).toBe('keepApart');
    const second = { ...PROPOSAL, id: 'p2', from: { ...PROPOSAL.from, name: 'Flaky tests' } };
    const two = context({ proposals: [PROPOSAL, second] });
    expect(failed(run(two, 'Merge them', [['answer_merge', { answer: 'merge' }]])).kind).toBe(
      'ambiguous',
    );
    const named = only(
      run(two, 'Merge flaky tests', [
        ['answer_merge', { answer: 'merge', proposal: 'flaky tests' }],
      ]),
    );
    expect(named.action).toMatchObject({ proposalId: 'p2' });
  });

  it('show changes nothing and gives the view intent', () => {
    for (const view of ['wentBadly', 'toDecide', 'features']) {
      const r = done(run(context(), 'Show it', [['show', { view }]]));
      expect(r.actions).toEqual([]);
      expect(r.lines).toEqual([]);
      expect(r.view).toEqual({ kind: 'screen', command: 'show', args: { view } });
    }
  });

  it('ask answers on screen from the points, by theme for what keeps going wrong', () => {
    const r = done(
      run(context(), 'What keeps going wrong?', [['ask', { topic: 'keepsGoingWrong' }]]),
    );
    expect(r.actions).toEqual([]);
    expect(r.view).toMatchObject({ kind: 'answer' });
    if (r.view?.kind !== 'answer') return;
    expect(r.view.content.rows.map((x) => x.title)).toEqual([
      'Slow builds',
      'Screenshot baselines need CI',
    ]);
    const left = done(
      run(context(), 'What is left to decide?', [['ask', { topic: 'leftToDecide' }]]),
    );
    if (left.view?.kind !== 'answer') throw new Error('no answer');
    expect(left.view.content.rows).toHaveLength(
      POINTS.filter((p) => p.decision === 'undecided').length,
    );
    const well = done(run(context(), 'What went well?', [['ask', { topic: 'whatWentWell' }]]));
    if (well.view?.kind !== 'answer') throw new Error('no answer');
    expect(well.view.content.rows.map((x) => x.title)).toEqual([
      'The contracts were on the branch before we started.',
      'Reviews came back within the hour.',
    ]);
  });

  it('hands a phrase that matches no point back as no match and several as ambiguous', () => {
    expect(
      failed(
        run(context(), 'Plus one the migration one', [['support', { point: 'the migration one' }]]),
      ).kind,
    ).toBe('noMatch');
    const f = failed(run(context(), 'Plus one baselines', [['support', { point: 'baselines' }]]));
    expect(f.kind).toBe('ambiguous');
  });

  it('refuses the web-only actions and returns hotwords and the screen keys', () => {
    expect(failed(run(context(), 'Rename the theme', [['rename_theme', {}]])).kind).toBe('webOnly');
    const spec = retroVoiceSpec(context({ lastTouchedId: byText('Profile').id }));
    expect(spec.hotwords()).toEqual(
      expect.arrayContaining([
        'Went well',
        'plus one',
        'not needed',
        'merge',
        'keep apart',
        'SHIP-958',
        'Priya Shah',
      ]),
    );
    const c = spec.context();
    expect(c.rows).toHaveLength(POINTS.length);
    expect(c.states).toEqual([]);
    expect(c.key).toBe(byText('Profile').workItem?.key ?? null);
    expect(retroVoiceSpec(context()).context().key).toBeNull();
  });

  describe('matchPoint', () => {
    it('matches every word case-insensitively', () => {
      expect(matchPoint('THE Signing KEY one', POINTS).map((p) => p.text)).toEqual([
        'The signing key expired mid-sprint.',
      ]);
      expect(matchPoint('signing build', POINTS)).toEqual([]);
      expect(matchPoint('the one', POINTS)).toEqual([]);
    });

    it('returns all matches for ambiguous phrases', () => {
      expect(matchPoint('baselines', POINTS)).toHaveLength(2);
    });
  });

  describe('test set', () => {
    const rows = readFileSync(new URL('../voice/testset/retro.jsonl', import.meta.url), 'utf8')
      .split('\n')
      .filter((l) => l !== '')
      .map((l) => JSON.parse(l) as TestSetRow);
    const KIND: Record<string, string> = {
      add_point: 'addPoint',
      add_point_in: 'addPoint',
      support: 'support',
      make_action: 'newAction',
      add_to_action: 'addToAction',
      not_needed: 'notNeeded',
    };

    it('has at least 40 rows, three per action, six that rows, four key rows and four failures', () => {
      expect(rows.length).toBeGreaterThanOrEqual(40);
      for (const a of RETRO_ACTIONS.filter((x) => x.webOnly === undefined))
        expect(
          rows.filter((r) => r.calls.some((c) => c.action === a.name)).length,
          a.name,
        ).toBeGreaterThanOrEqual(3);
      expect(
        rows.filter((r) =>
          r.calls.some((c) => c.args.point === 'that' || c.args.point === 'this one'),
        ).length,
      ).toBeGreaterThanOrEqual(6);
      expect(rows.filter((r) => /SHIP-\d+/.test(r.note ?? '')).length).toBeGreaterThanOrEqual(4);
      expect(rows.filter((r) => r.calls.length === 0)).toHaveLength(4);
    });

    it('refuses the likely wrong calls for the failure utterances', () => {
      const wrong: [string, string, [string, Record<string, unknown>][], string | undefined][] = [
        ['Um', 'Um', [['add_point', { note: 'x', category: 'wentBadly' }]], 'Um'],
        [
          'Never mind',
          'Never mind',
          [['add_point', { note: 'x', category: 'wentBadly' }]],
          'Never mind',
        ],
        [
          'Hmm, what was it',
          'Hmm, what was it',
          [['add_point', { note: 'x', category: 'wentBadly' }]],
          'what was it',
        ],
        [
          'Retro note',
          'Retro note',
          [['add_point', { note: 'x', category: 'wentBadly' }]],
          undefined,
        ],
      ];
      for (const [label, utterance, calls, note] of wrong)
        expect(failed(run(context(), utterance, calls, note)).kind, label).toBe('unclear');
    });

    it('replays every test-set row through toActions to its expected calls and note text', () => {
      const touched = byText('Profile');
      for (const row of rows) {
        const label = row.utterance;
        const result = run(
          context({ lastTouchedId: touched.id }),
          label,
          row.calls.map((c) => [c.action, c.args]),
          row.note ?? undefined,
        );
        if (row.calls.length === 0) {
          expect(failed(result).kind, label).toBe('unclear');
          continue;
        }
        const r = done(result);
        expect(r.rejected, label).toEqual([]);
        const call = row.calls[0];
        if (call.action === 'show' || call.action === 'ask') {
          expect(r.actions, label).toEqual([]);
          expect(r.view, label).not.toBeNull();
          continue;
        }
        const expected =
          call.action === 'answer_merge'
            ? call.args.answer === 'merge'
              ? 'mergeThemes'
              : 'keepApart'
            : KIND[call.action];
        expect(
          r.actions.map((a) => a.kind),
          label,
        ).toEqual([expected]);
        expect(r.lines.length, label).toBe(r.actions.length);
        const a = r.actions[0];
        if (a.kind === 'addPoint') expect(a.text, label).toBe(row.note);
        if (call.args.point === 'that' || call.args.point === 'this one') {
          const target =
            a.kind === 'support'
              ? a.pointId
              : a.kind === 'newAction' || a.kind === 'addToAction' || a.kind === 'notNeeded'
                ? 'pointIds' in a
                  ? a.pointIds[0]
                  : a.pointId
                : null;
          expect(target, label).toBe(touched.id);
        }
        if (a.kind === 'notNeeded' && call.args.reason !== undefined)
          expect(a.reason.toLowerCase(), label).toBe(String(call.args.reason).toLowerCase());
      }
    });
  });
});
