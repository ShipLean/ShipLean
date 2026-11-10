import { ApiRequestError } from '@shiplean/core/api/client';
import { markDigestSeen } from '@shiplean/core/api/featuresOverview';
import { getFeatureBoard } from '@shiplean/core/api/featureBoard';
import { getFeatureDigest, getFeaturesOverview } from '@shiplean/core/api/featuresOverview';
import { getStates } from '@shiplean/core/api/projectMeta';
import { answerQuestion, confirmDecisions } from '@shiplean/core/api/asks';
import { bulkPlace } from '@shiplean/core/api/backlog';
import { addComment, archiveComment } from '@shiplean/core/api/commentsApi';
import { setStatusLine } from '@shiplean/core/api/status';
import { getItemByKey, patchItem } from '@shiplean/core/api/workItem';
import type { WorkItemChanges } from '@shiplean/core/api/workItems';
import { afterEach, describe, expect, it } from 'vitest';
import { aProject } from './builders';
import { restoreFetch } from './fakeApi';
import { designOverview, FEATURE_STATES, featuresApi, NOTE_ID } from './features';

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

describe('featuresApi', () => {
  it('answers every route in its final shape', async () => {
    const overview = designOverview();
    const fake = featuresApi({}, { overview });
    const project = aProject().id;
    const row = overview.groups[0].rows[0];

    expect((await getFeaturesOverview(project)).groups).toHaveLength(4);
    expect((await getFeatureDigest(project)).needsYou).toHaveLength(1);
    await expect(markDigestSeen(project)).resolves.toBeUndefined();
    expect((await getFeatureBoard(project, row.key)).rows.length).toBeGreaterThan(0);
    const states = await getStates(project);
    expect(states).toHaveLength(5);
    expect(states.every((s) => s.type === 'feature')).toBe(true);
    const ids = states.map((s) => s.id);
    for (const r of overview.groups.flatMap((g) => g.rows)) expect(ids).toContain(r.state.id);
    const byKey = await getItemByKey(project, row.key);
    expect([byKey.id, byKey.key, byKey.title]).toEqual([row.id, row.key, row.title]);

    const patched = await patchItem(row.id, row.revision, {
      ...noChanges,
      statusLine: { value: 'Built.' },
    });
    expect(patched.key).toBe(row.key);
    const bulk = await bulkPlace(project, [
      {
        id: row.id,
        expectedRevision: row.revision,
        changes: null,
        placement: { slot: 'first', beforeItemId: null },
      },
    ]);
    expect(bulk.written.map((w) => w.id)).toEqual([row.id]);
    await expect(confirmDecisions(row.id, [1])).resolves.toBeUndefined();
    await expect(answerQuestion(row.id, 2, 'Yes.')).resolves.toBeUndefined();
    expect((await setStatusLine(row.id, row.revision, 'On track.')).id).toBe(row.id);
    const note = await addComment(row.id, 'A note.');
    expect(note.body).toBe('A note.');
    expect(note.id).toBe(NOTE_ID);
    await expect(archiveComment(NOTE_ID)).resolves.toBeUndefined();

    expect(fake.callsTo(`POST /api/projects/${project}/features/digest/seen`)).toHaveLength(1);
  });

  it('refuses what the server refuses', async () => {
    const overview = designOverview();
    featuresApi({}, { overview });
    const row = overview.groups[0].rows[0];
    const refused = async (changes: Partial<WorkItemChanges>) =>
      patchItem(row.id, row.revision, { ...noChanges, ...changes }).catch((e: unknown) => e);

    for (const text of ['x'.repeat(501), 'one\ntwo']) {
      const error = (await refused({ statusLine: { value: text } })) as ApiRequestError;
      expect(error).toBeInstanceOf(ApiRequestError);
      expect(error.status).toBe(400);
      expect(error.body).toMatchObject({ field: 'statusLine' });
    }
    const ok = await refused({ statusLine: { value: 'x'.repeat(500) } });
    expect(ok).not.toBeInstanceOf(Error);
    const state = (await refused({ state: { value: crypto.randomUUID() } })) as ApiRequestError;
    expect(state.status).toBe(400);
    expect(state.message).toBe("That state doesn't belong to this type of work item.");
    expect(await refused({ state: { value: FEATURE_STATES[1].id } })).not.toBeInstanceOf(Error);

    const empty = (await confirmDecisions(row.id, []).catch((e: unknown) => e)) as ApiRequestError;
    expect(empty.status).toBe(400);
    expect(empty.body).toMatchObject({ field: 'numbers' });
  });

  it('fails, goes offline and hangs on demand', async () => {
    const overview = designOverview();
    const fake = featuresApi({}, { overview });
    const project = aProject().id;
    const route = `GET /api/projects/${project}/features/overview`;

    fake.failNext(route, { status: 500, body: { error: 'Boom', code: 'internal_error' } });
    await expect(getFeaturesOverview(project)).rejects.toBeInstanceOf(ApiRequestError);
    await expect(getFeaturesOverview(project)).resolves.toBeTruthy();

    fake.offline(true);
    await expect(getFeaturesOverview(project)).rejects.toBeInstanceOf(TypeError);
    fake.offline(false);

    fake.hang(route);
    let settled = false;
    const sent = getFeaturesOverview(project).then(() => (settled = true));
    for (let i = 0; i < 20; i++) await Promise.resolve();
    expect(settled).toBe(false);
    fake.release(route);
    await sent;
    expect(settled).toBe(true);
  });
});
