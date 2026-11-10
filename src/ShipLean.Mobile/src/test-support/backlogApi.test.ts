import { addItems, bulkPlace, getBacklog, reorder } from '@shiplean/core/api/backlog';
import { confirmDecisions } from '@shiplean/core/api/asks';
import { addComment, archiveComment } from '@shiplean/core/api/commentsApi';
import { patchItem } from '@shiplean/core/api/workItem';
import { describe, expect, it } from 'vitest';
import { aBacklogView, backlogApi, designFeatures, designStories, NOTE_ID } from './backlog';
import { controlledApi } from './controlledApi';

const PROJECT = '00000002-0000-4000-8000-000000000001';
const list = { mode: 'list', filter: '', includeCompleted: false } as const;

describe('backlogApi', () => {
  it('answers every route in its final shape', async () => {
    controlledApi(backlogApi());
    const features = await getBacklog(PROJECT, 'features', list);
    expect(features.level).toBe('features');
    expect(features.rows).toHaveLength(7);
    expect(features.rows.filter((r) => r.needsYou !== null)).toHaveLength(3);
    expect(features.rows.filter((r) => r.blockedBy.length > 0)).toHaveLength(1);
    expect((await getBacklog(PROJECT, 'epics', list)).rows).toEqual([]);
    const stories = await getBacklog(PROJECT, 'stories', list);
    expect(stories.rows.some((r) => r.rollups.some((x) => x.type === 'task'))).toBe(true);

    const item = features.rows[0];
    expect(await reorder(item.id, 1, null)).toMatchObject({ id: item.id, key: item.key });
    const bulk = await bulkPlace(PROJECT, [
      {
        id: item.id,
        expectedRevision: 1,
        changes: null,
        placement: { slot: 'first', beforeItemId: null },
      },
    ]);
    expect(bulk.written).toHaveLength(1);
    expect(bulk.stale).toEqual([]);
    const added = await addItems(
      PROJECT,
      [{ type: 'story', title: 'T', description: null, points: null, parentId: null, tasks: [] }],
      null,
    );
    expect(added.created).toHaveLength(1);
    await expect(confirmDecisions(item.id, [1])).resolves.toBeUndefined();
    const note = await addComment(item.id, 'hello');
    expect(note).toMatchObject({ id: NOTE_ID, workItemId: item.id, body: 'hello' });
    await expect(archiveComment(note.id)).resolves.toBeUndefined();
    expect(await patchItem(item.id, 1, {} as never)).toMatchObject({ id: item.id });
  });

  it('builds rows with random ids and ranks in order', () => {
    const rows = designStories();
    expect(new Set(rows.map((r) => r.id)).size).toBe(rows.length);
    expect(rows.map((r) => r.rank)).toEqual(rows.map((_, i) => i + 1));
    expect(designFeatures()[0].id).not.toBe(designFeatures()[0].id);
    expect(aBacklogView('stories', rows).totalCount).toBe(rows.length);
  });

  it('fails, goes offline and hangs on demand', async () => {
    const api = controlledApi(backlogApi());
    const route = `GET /api/projects/${PROJECT}/backlogs/features`;
    api.failNext(route, { status: 500, body: { error: 'x', code: 'server' } });
    await expect(getBacklog(PROJECT, 'features', list)).rejects.toBeDefined();
    await expect(getBacklog(PROJECT, 'features', list)).resolves.toMatchObject({
      level: 'features',
    });

    api.offline(true);
    await expect(getBacklog(PROJECT, 'features', list)).rejects.toBeInstanceOf(TypeError);
    api.offline(false);

    api.hang(route);
    let done = false;
    const read = getBacklog(PROJECT, 'features', list).then(() => {
      done = true;
    });
    await Promise.resolve();
    expect(done).toBe(false);
    api.release(route);
    await read;
    expect(done).toBe(true);
  });
});
