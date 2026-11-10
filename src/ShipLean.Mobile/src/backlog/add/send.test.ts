import { ApiRequestError } from '@shiplean/core/api/client';
import type { StateDto } from '@shiplean/core/api/projects';
import { afterEach, describe, expect, it } from 'vitest';
import type { Api } from '../../actions/types';
import { aPending } from '../../test-support/actions';
import { aBacklogRow, aBacklogView, backlogApi, designStories } from '../../test-support/backlog';
import { controlledApi } from '../../test-support/controlledApi';
import { restoreFetch } from '../../test-support/fakeApi';
import type { BacklogAddItem } from '../actions';
import { describeAddItem, reconcileAddItem, sendAddItem, undoAddItem } from './send';

afterEach(restoreFetch);

// The runner's api argument is unused: the senders go through @shiplean/core, which reads the installed fake.
const api = {} as Api;
const PROJECT = '00000002-0000-4000-8000-000000000001';
const ADD = `POST /api/projects/${PROJECT}/backlog/items`;
const STORIES = `GET /api/projects/${PROJECT}/backlog`;
const STATES = `GET /api/projects/${PROJECT}/states`;
const FEATURE = { id: 'feature-906', key: 'SHIP-906' };

function aState(type: string, category: StateDto['category'], name: string): StateDto {
  return { id: `${type}-${name}`, type, name, category, order: 1, itemCount: 0 } as StateDto;
}
const states = [
  aState('story', 'proposed', 'New'),
  aState('story', 'removed', 'Removed story'),
  aState('bug', 'removed', 'Removed bug'),
  aState('feature', 'removed', 'Removed feature'),
];

function anAdd(overrides: Partial<BacklogAddItem> = {}): BacklogAddItem {
  return {
    area: 'backlog',
    kind: 'addItem',
    itemId: null,
    key: null,
    projectId: PROJECT,
    type: 'story',
    title: 'Offline queue for notes',
    parentId: FEATURE.id,
    parentKey: FEATURE.key,
    placement: { slot: 'first', beforeItemId: null },
    ...overrides,
  };
}

describe('backlog add executor', () => {
  it('sends one item at the top with no sprint and the parent', async () => {
    const fake = controlledApi(backlogApi());
    const receipt = await sendAddItem(anAdd(), api);
    const [call] = fake.callsTo(ADD);
    expect(call.body).toEqual({
      items: [
        {
          type: 'story',
          title: 'Offline queue for notes',
          description: null,
          points: null,
          parentId: FEATURE.id,
          tasks: [],
        },
      ],
      sprintId: null,
      placement: { slot: 'first', beforeItemId: null },
    });
    expect(receipt.createdId).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('sends a null placement as the top', async () => {
    const fake = controlledApi(backlogApi());
    await sendAddItem(anAdd({ placement: null }), api);
    expect((fake.callsTo(ADD)[0].body as { placement: unknown }).placement).toEqual({
      slot: 'first',
      beforeItemId: null,
    });
  });

  it('sends at the bottom when the action says so', async () => {
    const fake = controlledApi(backlogApi());
    await sendAddItem(anAdd({ placement: { slot: 'last', beforeItemId: null } }), api);
    expect((fake.callsTo(ADD)[0].body as { placement: unknown }).placement).toEqual({
      slot: 'last',
      beforeItemId: null,
    });
  });

  it('keeps the title exactly as given and hands back the new id', async () => {
    const fake = controlledApi(backlogApi({ [ADD]: () => ({ created: [{ id: 'new-1' }] }) }));
    const receipt = await sendAddItem(anAdd({ title: 'fix the  login — please' }), api);
    expect((fake.callsTo(ADD)[0].body as { items: { title: string }[] }).items[0].title).toBe(
      'fix the  login — please',
    );
    expect(receipt).toEqual({ createdId: 'new-1' });
  });

  it('describes the capture state: a new Story under SHIP-906 at the top', () => {
    const line = describeAddItem(anAdd());
    expect(line.text).toBe('New Story: "Offline queue for notes" · under SHIP-906 · at the top');
    expect(line.detail).toBe('Everything else moves down one.');
  });

  it('describes a bottom add and an add with no parent', () => {
    const line = describeAddItem(
      anAdd({
        type: 'feature',
        parentId: null,
        parentKey: null,
        placement: { slot: 'last', beforeItemId: null },
      }),
    );
    expect(line.text).toBe('New Feature: "Offline queue for notes" · at the bottom');
    expect(line.detail).toBeUndefined();
  });

  it('undoes a sent add by moving the new item to its Removed state', async () => {
    const row = aBacklogRow({
      id: 'new-1',
      key: 'SHIP-50',
      title: 'Offline queue for notes',
      revision: 4,
    });
    const fake = controlledApi(
      backlogApi({
        [STATES]: states,
        [STORIES]: aBacklogView('stories', [row, ...designStories()]),
        'PATCH /api/work-items/new-1': {
          id: 'new-1',
          key: 'SHIP-50',
          revision: 5,
          changes: [],
          notices: [],
        },
      }),
    );
    await undoAddItem(anAdd(), api, { createdId: 'new-1' });
    const [patch] = fake.callsTo('PATCH /api/work-items/new-1');
    expect(patch.body).toMatchObject({
      expectedRevision: 4,
      changes: { state: { value: 'story-Removed story' } },
    });
  });

  it('undoes a Feature through the Features level and its own Removed state', async () => {
    const row = aBacklogRow({
      id: 'f-new',
      title: 'Offline',
      type: { id: 'feature', name: 'Feature' },
      revision: 2,
    });
    const fake = controlledApi(
      backlogApi({
        [STATES]: states,
        [`GET /api/projects/${PROJECT}/backlogs/features`]: aBacklogView('features', [row]),
        'PATCH /api/work-items/f-new': {
          id: 'f-new',
          key: 'SHIP-51',
          revision: 3,
          changes: [],
          notices: [],
        },
      }),
    );
    await undoAddItem(anAdd({ type: 'feature', parentId: null, parentKey: null }), api, {
      createdId: 'f-new',
    });
    expect(fake.callsTo('PATCH /api/work-items/f-new')[0].body).toMatchObject({
      changes: { state: { value: 'feature-Removed feature' } },
    });
  });

  it('fails the undo when the receipt is null', async () => {
    const fake = controlledApi(backlogApi({ [STATES]: states }));
    await expect(undoAddItem(anAdd(), api, null)).rejects.toThrow();
    await expect(undoAddItem(anAdd(), api)).rejects.toThrow();
    expect(fake.calls.filter((c) => c.method === 'PATCH')).toHaveLength(0);
  });

  it('fails the undo when the new item is no longer on the level', async () => {
    const fake = controlledApi(backlogApi({ [STATES]: states }));
    await expect(undoAddItem(anAdd(), api, { createdId: 'gone' })).rejects.toThrow();
    expect(fake.calls.filter((c) => c.method === 'PATCH')).toHaveLength(0);
  });

  it('does not retry a 409 on the undo patch', async () => {
    const row = aBacklogRow({ id: 'new-1', revision: 4 });
    const fake = controlledApi(
      backlogApi({
        [STATES]: states,
        [STORIES]: aBacklogView('stories', [row]),
        'PATCH /api/work-items/new-1': {
          status: 409,
          body: { error: 'Changed.', code: 'conflict' },
        },
      }),
    );
    await expect(undoAddItem(anAdd(), api, { createdId: 'new-1' })).rejects.toBeInstanceOf(
      ApiRequestError,
    );
    expect(fake.callsTo('PATCH /api/work-items/new-1')).toHaveLength(1);
  });

  it('reconciles an uncertain add as sent when the row is there and notSent when not', async () => {
    const parent = {
      id: FEATURE.id,
      key: FEATURE.key,
      type: { id: 'feature', name: 'Feature' },
      title: 'F',
    } as never;
    const there = aBacklogRow({ title: 'Offline queue for notes', parent });
    const elsewhere = aBacklogRow({ title: 'Another one', parent: null });
    controlledApi(backlogApi({ [STORIES]: aBacklogView('stories', [there, elsewhere]) }));
    const pending = (a: BacklogAddItem) => aPending({ action: a });
    const outcomes = await reconcileAddItem(
      [
        pending(anAdd()),
        pending(anAdd({ title: 'Never arrived' })),
        pending(anAdd({ parentId: null, parentKey: null })),
        pending(anAdd({ title: 'Another one', parentId: null, parentKey: null })),
      ],
      api,
    );
    expect(outcomes).toEqual(['sent', 'notSent', 'notSent', 'sent']);
  });
});
