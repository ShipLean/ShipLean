import { ApiRequestError } from '@shiplean/core/api/client';
import { conflictOf } from '@shiplean/core/api/workItems';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { afterEach, describe, expect, it } from 'vitest';
import { rankPlan } from '../../actions/rankPlan';
import type { Api } from '../../actions/types';
import { aPending } from '../../test-support/actions';
import { aBacklogRow, backlogApi, designFeatures } from '../../test-support/backlog';
import { aConflict, aWorkItem } from '../../test-support/builders';
import { controlledApi } from '../../test-support/controlledApi';
import { restoreFetch } from '../../test-support/fakeApi';
import type { BacklogMove } from '../actions';
import { blockedWarning, plannedKeys } from './blockedBy';
import { rankRows } from './rankRows';
import { describeMove, reconcileMove, sendMove, undoMove } from './send';

afterEach(restoreFetch);

// The runner's api argument is unused: the senders go through @shiplean/core, which reads the installed fake.
const api = {} as Api;
const PROJECT = '00000002-0000-4000-8000-000000000001';
const BULK = `POST /api/projects/${PROJECT}/backlog/bulk`;
const FEATURES = `GET /api/projects/${PROJECT}/backlogs/features`;

// SHIP-906 (rank 4) above SHIP-907 (rank 2): 4 → 2, with SHIP-907 and SHIP-892 shifting down.
function world() {
  const before = designFeatures();
  const moved = before.find((r) => r.key === 'SHIP-906')!;
  const rows = rankRows(before);
  const plan = rankPlan(rows, moved.id, {
    to: 'above',
    otherId: before.find((r) => r.key === 'SHIP-907')!.id,
  })!;
  const action: BacklogMove = {
    area: 'backlog',
    kind: 'move',
    projectId: PROJECT,
    itemId: moved.id,
    key: moved.key,
    revision: 3,
    level: 'features',
    fromRank: plan.fromRank,
    toRank: plan.toRank,
    placement: plan.placement,
    undoPlacement: plan.undoPlacement,
    shifted: plan.shifted,
    warning: blockedWarning(moved.key, moved.blockedBy, plannedKeys(rows, moved.id, plan.toRank)),
  };
  // The level as the server reads it once the move is made, with SHIP-906 at `rank`.
  const after = (rank: number, revision = 4): BacklogRowDto[] => {
    const rest = before.filter((r) => r.id !== moved.id);
    const order = [...rest.slice(0, rank - 1), { ...moved, revision }, ...rest.slice(rank - 1)];
    return order.map((r, i) => ({ ...r, rank: i + 1 }));
  };
  return { before, moved, action, after };
}

describe('backlog move executor', () => {
  it('sends one position-only bulkPlace', async () => {
    const { action, moved } = world();
    const fake = controlledApi(backlogApi());
    await sendMove(action, api);
    const calls = fake.callsTo(BULK);
    expect(calls).toHaveLength(1);
    expect(calls[0].body).toEqual({
      items: [{ id: moved.id, expectedRevision: 3, changes: null, placement: action.placement }],
    });
  });
  });

  it('throws a 409 when the result reports a stale row', async () => {
    const { action, moved } = world();
    const conflict = aConflict(aWorkItem({ id: moved.id, key: moved.key, revision: 9 }));
    controlledApi(backlogApi({ [BULK]: { written: [], stale: [conflict] } }));
    const error = await sendMove(action, api).then(
      () => null,
      (e: unknown) => e,
    );
    expect(error).toBeInstanceOf(ApiRequestError);
    expect((error as ApiRequestError).status).toBe(409);
    expect(conflictOf(error)).toEqual(conflict);
  });

  it('undoes with the stored placement', async () => {
    const { action, after, moved } = world();
    const rows = after(2, 6);
    const fake = controlledApi(backlogApi({}, { rows: { features: rows } }));
    await undoMove(action, api);
    expect(fake.callsTo(FEATURES)).toHaveLength(1);
    const calls = fake.callsTo(BULK);
    expect(calls).toHaveLength(1);
    expect(calls[0].body).toEqual({
      items: [
        { id: moved.id, expectedRevision: 6, changes: null, placement: action.undoPlacement },
      ],
    });
    // Back above the row that followed it, SHIP-891.
    expect(action.undoPlacement).toEqual({
      slot: 'before',
      beforeItemId: rows.find((r) => r.key === 'SHIP-891')!.id,
    });
  });

  it('fails the undo when the item has moved since', async () => {
    const { action, after } = world();
    const fake = controlledApi(backlogApi({}, { rows: { features: after(5) } }));
    await expect(undoMove(action, api)).rejects.toThrow('SHIP-906 has moved since');
    expect(fake.callsTo(BULK)).toHaveLength(0);
  });

  it('fails the undo when a stale result comes back, without retrying', async () => {
    const { action, after, moved } = world();
    const conflict = aConflict(aWorkItem({ id: moved.id, key: moved.key }));
    const fake = controlledApi(
      backlogApi({ [BULK]: { written: [], stale: [conflict] } }, { rows: { features: after(2) } }),
    );
    await expect(undoMove(action, api)).rejects.toBeInstanceOf(ApiRequestError);
    expect(fake.callsTo(BULK)).toHaveLength(1);
  });

  it('reconciles an uncertain move as sent when the item is at its new rank, else notSent', async () => {
    const { action, after, before } = world();
    const other: BacklogMove = {
      ...action,
      itemId: before[0].id,
      key: before[0].key,
      toRank: 5,
    };
    const pendings = [
      aPending({ id: 'a', action }),
      aPending({ id: 'b', action: other }),
      aPending({ id: 'c', action: { ...action, itemId: aBacklogRow().id } }),
    ];
    const moved = controlledApi(backlogApi({}, { rows: { features: after(2) } }));
    expect(await reconcileMove(pendings, api)).toEqual(['sent', 'notSent', 'notSent']);
    expect(moved.callsTo(FEATURES)).toHaveLength(1);
    restoreFetch();
    controlledApi(backlogApi({}, { rows: { features: before } }));
    expect(await reconcileMove([pendings[0]], api)).toEqual(['notSent']);
  });

  it('describes the move with its shifted rows and the warning', () => {
    const { action } = world();
    expect(describeMove(action)).toMatchObject({
      text: 'SHIP-906 4 → 2',
      detail: 'SHIP-907 2 → 3\nSHIP-892 3 → 4',
      warning: 'SHIP-906 is blocked by SHIP-907',
      removable: true,
    });
    expect(describeMove({ ...action, shifted: [], warning: null }).warning).toBeUndefined();
  });
});
