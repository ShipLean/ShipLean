import { ApiRequestError } from '@shiplean/core/api/client';
import { getItemByKey, patchItem } from '@shiplean/core/api/workItem';
import { getSprints } from '@shiplean/core/api/projectMeta';
import {
  addPointsToRetroAction,
  archiveRetroPoint,
  createRetroPoint,
  fetchRetroActionDialog,
  fetchRetroFeatures,
  fetchRetroInsights,
  fetchSprintRetro,
  keepThemeProposalApart,
  markPointNotNeeded,
  mergeThemeProposal,
  newRetroAction,
  supportPoint,
  undoPointNotNeeded,
  unsupportPoint,
} from '@shiplean/core/api/retrosApi';
import { afterEach, describe, expect, it } from 'vitest';
import { aProject } from './builders';
import { restoreFetch } from './fakeApi';
import { designInsights, designRetro, retroApi } from './retro';

afterEach(restoreFetch);

describe('retroApi', () => {
  it('answers every route in its final shape', async () => {
    const retro = designRetro();
    const insights = designInsights();
    const fake = retroApi(
      {},
      { retro, insights, newAction: { workItemId: 'wi-1', key: 'SHIP-901' } },
    );
    const project = aProject().id;
    const point = retro.categories[2].groups[0].points[0];

    expect((await getSprints(project))[0].name).toBe('Sprint 25');
    expect((await fetchSprintRetro(retro.sprint.id)).sprint.name).toBe('Sprint 25');
    expect((await fetchRetroInsights(project, '6')).mergeProposals).toHaveLength(1);
    expect((await fetchRetroFeatures(project)).features).toHaveLength(5);
    const dialog = await fetchRetroActionDialog(project, { pointIds: [point.id] });
    expect(dialog.openActions[0].key).toBe('SHIP-901');

    const created = await createRetroPoint(project, {
      retroId: null,
      workItemId: null,
      sprintId: retro.sprint.id,
      category: 'improve',
      text: 'Profile first.',
      rootCause: null,
      themeId: null,
      themeName: null,
    });
    expect(created.text).toBe('Profile first.');
    expect(created.category).toBe('improve');
    await archiveRetroPoint(created.id);
    expect(fake.callsTo(`POST /api/projects/${project}/retros/points`)).toHaveLength(1);

    await supportPoint(point.id);
    await unsupportPoint(point.id);
    await archiveRetroPoint(point.id);
    await expect(markPointNotNeeded(point.id, { reason: '' })).rejects.toBeInstanceOf(
      ApiRequestError,
    );
    expect((await markPointNotNeeded(point.id, { reason: 'Not needed' })).decision).toBe(
      'notNeeded',
    );
    expect((await undoPointNotNeeded(point.id)).decision).toBe('undecided');

    const action = await newRetroAction(project, {
      title: 'Profile',
      fixType: 'guide',
      pointIds: [point.id],
      themeId: null,
      themeName: null,
    });
    expect(action.action.workItemId).toBe('wi-1');
    await addPointsToRetroAction('wi-1', { pointIds: [point.id] });
    const item = await getItemByKey(project, 'SHIP-901');
    expect(item.key).toBe('SHIP-901');
    const write = await patchItem('wi-1', item.revision, {
      title: null,
      description: null,
      acceptanceCriteria: null,
      reproSteps: null,
      severity: null,
      state: null,
      assignee: { value: 'user-1' },
      sprint: null,
      priority: null,
      points: null,
      parent: null,
      tags: null,
      addLinks: [],
      removeLinkIds: [],
    });
    expect(write.revision).toBeGreaterThan(0);

    await mergeThemeProposal(insights.mergeProposals[0].id);
    await keepThemeProposalApart(insights.mergeProposals[0].id);
    expect(fake.calls.every((c) => c.path.startsWith('/api/'))).toBe(true);
  });

  it('fails, goes offline and hangs on demand', async () => {
    const retro = designRetro();
    const fake = retroApi({}, { retro });
    const id = retro.categories[0].groups[0].points[0].id;
    const route = `POST /api/retros/points/${id}/support`;

    fake.failNext(route, { status: 500, body: { error: 'Boom', code: 'internal_error' } });
    await expect(supportPoint(id)).rejects.toBeInstanceOf(ApiRequestError);
    await expect(supportPoint(id)).resolves.toBeUndefined();

    fake.offline(true);
    await expect(supportPoint(id)).rejects.toBeInstanceOf(TypeError);
    fake.offline(false);

    fake.hang(route);
    let settled = false;
    const sent = supportPoint(id).then(() => (settled = true));
    for (let i = 0; i < 20; i++) await Promise.resolve();
    expect(settled).toBe(false);
    fake.release(route);
    await sent;
    expect(settled).toBe(true);
  });
});
