import { ApiRequestError } from '@shiplean/core/api/client';
import { afterEach, describe, expect, it } from 'vitest';
import type { Api, Pending } from '../../actions/types';
import { aPending } from '../../test-support/actions';
import { aMe, aProject, aStory } from '../../test-support/builders';
import { restoreFetch } from '../../test-support/fakeApi';
import { aRetroPoint, designRetro, retroApi } from '../../test-support/retro';
import type { PointAction } from '../actions';
import { retroExecutor } from '../executor';
import { describePoint, reconcilePoint, sendPoint, undoPoint } from './send';

afterEach(restoreFetch);

// The executor sends through @shiplean/core, which reads the installed fake at fetch; the api argument is unused.
const api = {} as Api;
const projectId = aProject().id;
const POINTS = `POST /api/projects/${projectId}/retros/points`;

type AddPoint = Extract<PointAction, { kind: 'addPoint' }>;
const add = (overrides: Partial<AddPoint> = {}): AddPoint => ({
  area: 'retro',
  kind: 'addPoint',
  itemId: null,
  key: null,
  projectId,
  sprintId: 'sprint-25',
  category: 'improve',
  categoryGuessed: false,
  text: 'Profile the small model first.',
  ...overrides,
});
const byKey = (key: string) => `GET /api/projects/${projectId}/work-items/by-key/${key}`;

describe('retro point executor', () => {
  it('adds the point about the named item', async () => {
    const item = aStory({ key: 'SHIP-906' });
    const fake = retroApi({ [byKey('SHIP-906')]: item }, { projectId, createdPointId: 'new-1' });
    // The action carries a sprint too: the point must go about the item alone.
    const receipt = await sendPoint(
      add({
        key: 'SHIP-906',
        sprintId: 'sprint-25',
        category: 'wentBadly',
        text: ' Slow builds. ',
      }),
      api,
    );
    expect(fake.callsTo(POINTS).map((c) => c.body)).toEqual([
      {
        retroId: null,
        workItemId: item.id,
        sprintId: null,
        category: 'wentBadly',
        text: 'Slow builds.',
        rootCause: null,
        themeId: null,
        themeName: null,
      },
    ]);
    expect(receipt).toEqual({ createdId: 'new-1' });
  });

  it('throws the 404 for an unknown key and never adds it about the sprint', async () => {
    const fake = retroApi({}, { projectId });
    const failed = await sendPoint(add({ key: 'SHIP-9999', sprintId: 'sprint-25' }), api).catch(
      (e: unknown) => e,
    );
    expect(failed).toBeInstanceOf(ApiRequestError);
    expect((failed as ApiRequestError).status).toBe(404);
    expect(fake.callsTo(POINTS)).toEqual([]);
  });

  it('adds a typed point about the sprint', async () => {
    const fake = retroApi({}, { projectId });
    await sendPoint(
      add({ text: '  Reviews came back within the hour.  ', category: 'wentWell' }),
      api,
    );
    expect(fake.callsTo(POINTS).map((c) => c.body)).toEqual([
      {
        retroId: null,
        workItemId: null,
        sprintId: 'sprint-25',
        category: 'wentWell',
        text: 'Reviews came back within the hour.',
        rootCause: null,
        themeId: null,
        themeName: null,
      },
    ]);
  });

  it('undoes an added point by archiving the receipt createdId, and fails with no receipt', async () => {
    const fake = retroApi({}, { projectId, createdPointId: 'new-1' });
    const receipt = await sendPoint(add(), api);
    await undoPoint(add(), api, receipt ?? null);
    expect(fake.callsTo('POST /api/retros/points/new-1/archive')).toHaveLength(1);

    const before = fake.calls.length;
    await expect(undoPoint(add(), api, null)).rejects.toThrow(/no receipt/);
    await expect(undoPoint(add(), api)).rejects.toThrow(/no receipt/);
    expect(fake.calls).toHaveLength(before);
  });

  it('reconciles an uncertain point as sent when it is in the retro, else notSent', async () => {
    const me = aMe();
    const retro = designRetro();
    const mine = aRetroPoint({
      category: 'improve',
      text: 'Profile the small model first.',
      author: { ...aRetroPoint().author, user: { ...aRetroPoint().author.user, id: me.id } },
    });
    retro.categories[3].groups.push({ theme: null, recurring: false, points: [mine] });
    retroApi({ 'GET /api/me': me }, { projectId, retro });
    const pending = (a: AddPoint): Pending =>
      aPending({ action: a, state: 'uncertain', retryable: true });
    const answers = await reconcilePoint(
      [
        pending(add({ sprintId: retro.sprint.id })),
        // Same text, another category: not the same point.
        pending(add({ sprintId: retro.sprint.id, category: 'wentBadly' })),
        pending(add({ sprintId: retro.sprint.id, text: 'Something never sent.' })),
      ],
      api,
    );
    expect(answers).toEqual(['sent', 'notSent', 'notSent']);
  });

  it('does not count someone else’s identical point when reconciling', async () => {
    const retro = designRetro();
    const theirs = aRetroPoint({ category: 'improve', text: 'Profile the small model first.' });
    retro.categories[3].groups.push({ theme: null, recurring: false, points: [theirs] });
    retroApi({ 'GET /api/me': aMe() }, { projectId, retro });
    const answers = await reconcilePoint(
      [aPending({ action: add({ sprintId: retro.sprint.id }), state: 'uncertain' })],
      api,
    );
    expect(answers).toEqual(['notSent']);
  });

  it('supports and unsupports, and undoes one with the other', async () => {
    const retro = designRetro();
    const id = retro.categories[2].groups[0].points[0].id;
    const fake = retroApi({}, { projectId, retro });
    const route = (method: string) => `${method} /api/retros/points/${id}/support`;
    const support: PointAction = {
      area: 'retro',
      kind: 'support',
      itemId: null,
      key: null,
      pointId: id,
    };
    const unsupport: PointAction = { ...support, kind: 'unsupport' };

    expect(await sendPoint(support, api)).toBeUndefined();
    await undoPoint(support, api);
    expect(fake.callsTo(route('POST'))).toHaveLength(1);
    expect(fake.callsTo(route('DELETE'))).toHaveLength(1);

    expect(await sendPoint(unsupport, api)).toBeUndefined();
    await undoPoint(unsupport, api);
    expect(fake.callsTo(route('DELETE'))).toHaveLength(2);
    expect(fake.callsTo(route('POST'))).toHaveLength(2);

    expect(retroExecutor.reversible('support')).toBe(true);
  });

  it('describes the dictate-preview state with the picked-category detail', () => {
    const line = describePoint(
      add({
        key: 'SHIP-906',
        sprintId: null,
        category: 'improve',
        categoryGuessed: true,
        text: 'We should profile the small model on the S22.',
      }),
    );
    expect(line.text).toBe(
      'Can be improved: "We should profile the small model on the S22." · about SHIP-906',
    );
    expect(line.detail).toBe('Category picked for you');
    const typed = describePoint(add({ category: 'wentBadly', text: 'Slow builds.' }));
    expect(typed.text).toBe('Went badly: "Slow builds." · about this sprint');
    expect(typed.detail).toBeUndefined();
  });
});
