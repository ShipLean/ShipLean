import {
  answerQuestion,
  changeDecision,
  confirmDecisions,
  discussDecision,
  getItemAsks,
} from '@shiplean/core/api/asks';
import { ApiRequestError } from '@shiplean/core/api/client';
import { addComment, archiveComment, listComments } from '@shiplean/core/api/commentsApi';
import { getFeatureBoard } from '@shiplean/core/api/featureBoard';
import { getHistory } from '@shiplean/core/api/history';
import { getMembers, getStates } from '@shiplean/core/api/projectMeta';
import { confirmStatus, setStatusLine } from '@shiplean/core/api/status';
import { getChildren, getItemByKey, patchItem } from '@shiplean/core/api/workItem';
import { conflictOf, type WorkItemChanges } from '@shiplean/core/api/workItems';
import { afterEach, describe, expect, it } from 'vitest';
import { aProject } from './builders';
import { restoreFetch } from './fakeApi';
import { aFeatureItem, aStoryItem, workItemApi } from './workItem';

afterEach(restoreFetch);

const noChanges: WorkItemChanges = {
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: null,
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
};

const project = aProject().id;

async function rejection(promise: Promise<unknown>): Promise<ApiRequestError> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof ApiRequestError) return error;
    throw error;
  }
  throw new Error('Expected the request to be refused.');
}

describe('workItemApi', () => {
  it('answers every route in its final shape', async () => {
    const feature = aFeatureItem({ projectId: project });
    const story = aStoryItem({ projectId: project });
    const fake = workItemApi({}, { items: [feature, story] });

    expect((await getItemByKey(project, 'SHIP-906')).health).toBe('atRisk');
    expect((await getItemByKey(project, 'SHIP-921')).rollups[0].type).toBe('task');
    expect((await getItemAsks(feature.id)).openDecisions).toBe(1);
    expect((await getHistory(feature.id)).revisions.length).toBeGreaterThan(0);
    expect(await listComments(feature.id)).toEqual([]);
    expect((await getChildren(story.id)).length).toBe(story.children.length);
    expect((await getStates(project)).length).toBeGreaterThan(0);
    expect((await getMembers(project)).length).toBeGreaterThan(0);
    expect((await getFeatureBoard(project, 'SHIP-906')).header.key).toBe('SHIP-906');

    const patched = await patchItem(feature.id, feature.revision, { ...noChanges, state: null });
    expect(patched).toMatchObject({ id: feature.id, revision: feature.revision + 1 });
    const line = await setStatusLine(feature.id, feature.revision + 1, 'Built.');
    expect(line.revision).toBe(feature.revision + 2);
    const confirmed = await confirmStatus(feature.id, feature.revision + 2);
    expect(confirmed).toMatchObject({ key: 'SHIP-906', revision: feature.revision + 3 });

    const note = await addComment(feature.id, 'A note.');
    expect(note).toMatchObject({ body: 'A note.', workItemId: feature.id });
    expect(await listComments(feature.id)).toHaveLength(1);
    await expect(archiveComment(note.id)).resolves.toBeUndefined();

    await expect(discussDecision(feature.id, 1, 'Why?')).resolves.toBeUndefined();
    await expect(answerQuestion(feature.id, 1, 'Yes.')).resolves.toBeUndefined();
    await expect(confirmDecisions(feature.id, [1])).resolves.toBeUndefined();
    const asks = await getItemAsks(feature.id);
    expect(asks.decisions[0].state).toBe('confirmed');
    expect(asks.questions[0].state).toBe('answered');

    expect(fake.callsTo(`GET /api/work-items/${feature.id}/asks`)).toHaveLength(2);
  });

  it('refuses what the server refuses', async () => {
    const feature = aFeatureItem({ projectId: project });
    workItemApi({}, { items: [feature] });

    const stale = await rejection(patchItem(feature.id, feature.revision - 1, noChanges));
    expect(stale.status).toBe(409);
    expect(conflictOf(stale)?.current.revision).toBe(feature.revision);
    expect((await rejection(confirmStatus(feature.id, 1))).status).toBe(409);

    expect((await rejection(addComment(feature.id, '  '))).status).toBe(400);
    expect((await rejection(archiveComment('00000008-0000-4000-8001-0000000000f1'))).status).toBe(
      404,
    );
    expect((await rejection(confirmDecisions(feature.id, []))).status).toBe(400);
    expect((await rejection(confirmDecisions(feature.id, [7]))).status).toBe(404);
    expect((await rejection(discussDecision(feature.id, 1, ''))).status).toBe(400);
    expect((await rejection(answerQuestion(feature.id, 1, ' '))).status).toBe(400);

    await confirmDecisions(feature.id, [1]);
    expect((await rejection(confirmDecisions(feature.id, [1]))).status).toBe(409);
    await answerQuestion(feature.id, 1, 'Yes.');
    expect((await rejection(answerQuestion(feature.id, 1, 'Again.'))).status).toBe(409);
    // changeDecision has no fake route: an unlisted request answers 404.
    expect(
      (
        await rejection(
          changeDecision(feature.id, 1, {
            kind: 'own',
            alternativeIndex: null,
            text: 'x',
            note: null,
          }),
        )
      ).status,
    ).toBe(404);
  });

  it('fails, goes offline and hangs on demand', async () => {
    const fake = workItemApi({}, { items: [aFeatureItem({ projectId: project })] });
    const route = 'GET /api/projects/' + project + '/work-items/by-key/SHIP-906';

    fake.failNext(route, { status: 500, body: { error: 'Boom', code: 'internal_error' } });
    await expect(getItemByKey(project, 'SHIP-906')).rejects.toBeInstanceOf(ApiRequestError);
    await expect(getItemByKey(project, 'SHIP-906')).resolves.toBeTruthy();

    fake.offline(true);
    await expect(getItemByKey(project, 'SHIP-906')).rejects.toBeInstanceOf(TypeError);
    fake.offline(false);

    fake.hang(route);
    let settled = false;
    const sent = getItemByKey(project, 'SHIP-906').then(() => (settled = true));
    for (let i = 0; i < 20; i++) await Promise.resolve();
    expect(settled).toBe(false);
    fake.release(route);
    await sent;
    expect(settled).toBe(true);
  });
});
