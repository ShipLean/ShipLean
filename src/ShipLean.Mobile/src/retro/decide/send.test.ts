import type {
  RetroActionDetailDto,
  RetroActionRefDto,
  RetroPointDto,
  SprintRetroDto,
} from '@shiplean/core/api/retros';
import { afterEach, describe, expect, it } from 'vitest';
import actionDetail from '../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-action-detail.json';
import type { Api } from '../../actions/types';
import { aPending } from '../../test-support/actions';
import { aMember, aProject } from '../../test-support/builders';
import { apiError, restoreFetch } from '../../test-support/fakeApi';
import { designRetro, retroApi } from '../../test-support/retro';
import type { DecisionAction } from '../actions';
import { retroExecutor } from '../executor';
import { describeDecision, reconcileDecision, sendDecision, undoDecision } from './send';

afterEach(restoreFetch);

// The sender goes through @shiplean/core, which reads the installed fake at fetch; the api argument is unused.
const api = {} as Api;
const projectId = aProject().id;
const ACTIONS = `POST /api/projects/${projectId}/retros/actions`;
const WORK_ITEM = 'work-item-958';

const retro = designRetro();
const [first, second] = retro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
const badly = retro.categories
  .find((c) => c.category === 'wentBadly')!
  .groups.flatMap((g) => g.points)[0];

const SPRINT_RETRO = `GET /api/sprints/${retro.sprint.id}/retro`;
const made: RetroActionRefDto = {
  ...(actionDetail as RetroActionDetailDto).action,
  workItemId: WORK_ITEM,
  key: 'SHIP-958',
};
const priya = { ...aMember().user, id: 'user-priya', name: 'Priya Shah' };

// The sprint retro as the server shows it once a point is decided.
const withDecided = (pointId: string, changes: Partial<RetroPointDto>): SprintRetroDto => ({
  ...retro,
  categories: retro.categories.map((c) => ({
    ...c,
    groups: c.groups.map((g) => ({
      ...g,
      points: g.points.map((p) => (p.id === pointId ? { ...p, ...changes } : p)),
    })),
  })),
});

const newAction = (overrides: Partial<Extract<DecisionAction, { kind: 'newAction' }>> = {}) =>
  ({
    area: 'retro',
    kind: 'newAction',
    itemId: null,
    key: null,
    projectId,
    sprintId: retro.sprint.id,
    pointIds: [badly.id],
    title: badly.text,
    ownerId: 'user-priya',
    ownerName: 'Priya Shah',
    themeId: badly.theme?.id ?? null,
    ...overrides,
  }) satisfies DecisionAction;
const addToAction = (): DecisionAction => ({
  area: 'retro',
  kind: 'addToAction',
  itemId: WORK_ITEM,
  key: 'SHIP-958',
  projectId,
  pointIds: [badly.id],
});
const notNeeded = (reason: string, pointId: string = badly.id): DecisionAction => ({
  area: 'retro',
  kind: 'notNeeded',
  itemId: null,
  key: null,
  projectId,
  sprintId: retro.sprint.id,
  pointId,
  reason,
});

describe('retro decision executor', () => {
  it('creates the action with the point text and guide, then assigns the owner', async () => {
    const fake = retroApi(
      {},
      { projectId, retro, newAction: { workItemId: WORK_ITEM, key: 'SHIP-958' } },
    );
    await sendDecision(newAction(), api);
    expect(fake.callsTo(ACTIONS).map((c) => c.body)).toEqual([
      {
        title: badly.text,
        fixType: 'guide',
        pointIds: [badly.id],
        themeId: badly.theme?.id ?? null,
        themeName: null,
      },
    ]);
    const patches = fake.callsTo(`PATCH /api/work-items/${WORK_ITEM}`);
    expect(patches).toHaveLength(1);
    expect(patches[0].body).toMatchObject({ changes: { assignee: { value: 'user-priya' } } });
    // The read of the new action item comes between the create and the assign, for its revision.
    const order = fake.calls.map((c) => `${c.method} ${c.path}`);
    expect(
      order.indexOf(`GET /api/projects/${projectId}/work-items/by-key/SHIP-958`),
    ).toBeGreaterThan(order.indexOf(ACTIONS));

    // No owner: only the create.
    const noOwner = retroApi(
      {},
      { projectId, retro, newAction: { workItemId: WORK_ITEM, key: 'SHIP-958' } },
    );
    await sendDecision(newAction({ ownerId: null, ownerName: null }), api);
    expect(noOwner.callsTo(ACTIONS)).toHaveLength(1);
    expect(noOwner.callsTo(`PATCH /api/work-items/${WORK_ITEM}`)).toHaveLength(0);
  });

  it('adds points to an existing action', async () => {
    const fake = retroApi(
      {},
      { projectId, retro, newAction: { workItemId: WORK_ITEM, key: 'SHIP-958' } },
    );
    await sendDecision(addToAction(), api);
    expect(fake.callsTo(`POST /api/retros/actions/${WORK_ITEM}/points`).map((c) => c.body)).toEqual(
      [{ pointIds: [badly.id] }],
    );
    expect(fake.callsTo(ACTIONS)).toHaveLength(0);
  });

  it('marks not needed with an empty reason and undoes it', async () => {
    const fake = retroApi({}, { projectId, retro });
    const path = `/api/retros/points/${badly.id}`;
    await sendDecision(notNeeded(''), api);
    await sendDecision(notNeeded('  '), api);
    await sendDecision(notNeeded('Fixed already in S23'), api);
    expect(fake.callsTo(`POST ${path}/not-needed`).map((c) => c.body)).toEqual([
      { reason: 'Not needed' },
      { reason: 'Not needed' },
      { reason: 'Fixed already in S23' },
    ]);
    await undoDecision(notNeeded(''), api);
    expect(fake.callsTo(`POST ${path}/undo-not-needed`)).toHaveLength(1);
  });

  it('says what stays after Undo: a new action item and added points have no reverse', async () => {
    expect(retroExecutor.reversible('notNeeded')).toBe(true);
    expect(retroExecutor.reversible('newAction')).toBe(false);
    expect(retroExecutor.reversible('addToAction')).toBe(false);
    await expect(undoDecision(newAction(), api)).rejects.toThrow('no undo');
  });

  it('describes each decision as the design words it', () => {
    expect(describeDecision(newAction({ title: 'Profile first' }))).toMatchObject({
      text: 'New action item "Profile first" · Priya Shah',
    });
    expect(describeDecision(newAction({ title: 'Profile first', ownerName: null }))).toMatchObject({
      text: 'New action item "Profile first"',
    });
    expect(describeDecision(addToAction()).text).toBe('Add to SHIP-958');
    expect(describeDecision(notNeeded('Fixed already')).text).toBe('Not needed · "Fixed already"');
    expect(describeDecision(notNeeded('')).text).toBe('Not needed');
  });

  it('reconciles an uncertain newAction as sent when the points already have an action, so no duplicate is made', async () => {
    const pending = aPending({
      action: newAction({ ownerId: null, ownerName: null }),
      state: 'uncertain',
    });
    const fake = retroApi({}, { projectId, retro: withDecided(badly.id, { action: made }) });
    await expect(reconcileDecision([pending], api)).resolves.toEqual(['sent']);
    expect(fake.callsTo(SPRINT_RETRO)).toHaveLength(1);

    // Still undecided: the create never happened, so a resend is safe.
    retroApi({}, { projectId, retro });
    await expect(reconcileDecision([pending], api)).resolves.toEqual(['notSent']);
  });

  it('reconciles a newAction with an owner as sent only when the action is assigned to the owner', async () => {
    const pending = aPending({ action: newAction(), state: 'uncertain' });
    const unassigned = withDecided(badly.id, { action: made });
    retroApi({}, { projectId, retro: unassigned });
    await expect(reconcileDecision([pending], api)).resolves.toEqual(['notSent']);

    const assigned: SprintRetroDto = {
      ...unassigned,
      actionItems: [{ action: made, theme: null, raisedIn: null, owner: priya, impact: null }],
    };
    retroApi({}, { projectId, retro: assigned });
    await expect(reconcileDecision([pending], api)).resolves.toEqual(['sent']);
  });

  it('reconciles addToAction by the action item and notNeeded by the sprint retro, read once', async () => {
    const detail = {
      action: { workItemId: WORK_ITEM, key: 'SHIP-958' },
      points: [badly],
    };
    const fake = retroApi(
      { [`GET /api/retros/actions/${WORK_ITEM}`]: detail },
      { projectId, retro: withDecided(badly.id, { decision: 'notNeeded' }) },
    );
    const add = aPending({ action: addToAction(), state: 'uncertain' });
    const decided = aPending({ action: notNeeded('x'), state: 'uncertain' });
    const undecided = aPending({ action: notNeeded('x', first.id), state: 'uncertain' });
    await expect(reconcileDecision([add, decided, undecided], api)).resolves.toEqual([
      'sent',
      'sent',
      'notSent',
    ]);
    expect(fake.callsTo(SPRINT_RETRO)).toHaveLength(1);
    expect(fake.callsTo('GET /api/projects')).toHaveLength(0);

    // A point decided another way (an action item) is not a Not needed.
    retroApi(
      { [`GET /api/retros/actions/${WORK_ITEM}`]: { ...detail, points: [second] } },
      { projectId, retro: withDecided(badly.id, { decision: 'actioned' }) },
    );
    await expect(reconcileDecision([decided], api)).resolves.toEqual(['notSent']);
    await expect(reconcileDecision([add], api)).resolves.toEqual(['notSent']);
  });

  it('retries a newAction whose owner assign failed without making a second action item', async () => {
    const fake = retroApi(
      {},
      { projectId, retro, newAction: { workItemId: WORK_ITEM, key: 'SHIP-958' } },
    );
    fake.failNext(
      `PATCH /api/work-items/${WORK_ITEM}`,
      apiError(500, 'server', 'The assign failed.'),
    );
    await expect(sendDecision(newAction(), api)).rejects.toThrow();
    expect(fake.callsTo(ACTIONS)).toHaveLength(1);

    // The server now shows the points carrying the action item the first send made.
    const after = retroApi(
      {},
      {
        projectId,
        retro: withDecided(badly.id, { action: made }),
        newAction: { workItemId: WORK_ITEM, key: 'SHIP-958' },
      },
    );
    await sendDecision(newAction(), api);
    expect(after.callsTo(ACTIONS)).toHaveLength(0);
    const patches = after.callsTo(`PATCH /api/work-items/${WORK_ITEM}`);
    expect(patches).toHaveLength(1);
    expect(patches[0].body).toMatchObject({ changes: { assignee: { value: 'user-priya' } } });
  });
});
