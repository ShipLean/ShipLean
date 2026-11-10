import type { BacklogRowDto } from '@shiplean/core/api/views';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { UNDO } from '../actions/undo';
import {
  backlogMoveButtonId,
  backlogRowId,
  moveApplyId,
  moveOptionId,
  movePickId,
  movePreviewPosId,
  moveSideId,
} from '../backlog/testIds';
import {
  featureDigestIds,
  featureDigestItemId,
  featureHealthButtonId,
  featureHealthOptionId,
  featureRowId,
  featuresIds,
} from '../features/testIds';
import {
  retroAddActionId,
  retroDecideOwnerId,
  retroIds,
  retroPlusId,
  retroTileId,
} from '../retro/testIds';
import { projectRowId } from '../screens/projects/testIds';
import { screenIds, tabIds } from '../screens/testIds';
import { aBacklogView, backlogApi, designFeatures } from '../test-support/backlog';
import { aMe, aMember, aProject } from '../test-support/builders';
import { designOverview, featuresApi } from '../test-support/features';
import { designRetro, retroApi } from '../test-support/retro';
import { controlledApi } from '../test-support/controlledApi';
import type { FakeRequest } from '../test-support/fakeApi';
import { renderApp } from '../test-support/renderApp';
import { signedInApi } from '../test-support/signedInApi';
import { kvValues } from '../test-support/storage';
import { optionId, primitiveIds } from '../ui/testIds';

const PROJECT = aProject().id;

const advance = (ms: number) => act(async () => jest.advanceTimersByTimeAsync(ms));

// From Projects: tap SHIP, then the tab in the tab bar.
async function openTab(
  api: ReturnType<typeof controlledApi>,
  tab: 'backlog' | 'features' | 'retro',
) {
  await renderApp({ url: '/', api });
  await fireEvent.press(await screen.findByTestId(projectRowId('SHIP')));
  await fireEvent.press(await screen.findByTestId(tabIds[tab]));
}

// The fake server keeps the order: a bulk placement moves the row before the one named, and the next read shows it.
function backlogJourneyApi() {
  const order = designFeatures();
  const features = `GET /api/projects/${PROJECT}/backlogs/features`;
  const bulk = `POST /api/projects/${PROJECT}/backlog/bulk`;
  const api = controlledApi({
    ...signedInApi(),
    ...backlogApi(
      {
        [features]: () =>
          aBacklogView(
            'features',
            order.map((r, i) => ({ ...r, rank: i + 1 })),
          ),
        [bulk]: (request: FakeRequest) => {
          const [item] = (
            request.body as {
              items: { id: string; placement: { slot: string; beforeItemId?: string } }[];
            }
          ).items;
          const from = order.findIndex((r) => r.id === item!.id);
          const [moved] = order.splice(from, 1) as [BacklogRowDto];
          const to = order.findIndex((r) => r.id === item!.placement.beforeItemId);
          order.splice(to, 0, moved);
          return { written: [], stale: [] };
        },
      },
      { projectId: PROJECT },
    ),
    [`GET /api/projects/${PROJECT}/members`]: [],
  });
  return { api, order, features, bulk };
}

async function applyMoveAboveShip907(api: ReturnType<typeof controlledApi>) {
  await openTab(api, 'backlog');
  expect(await screen.findByTestId(backlogRowId('SHIP-906'))).toBeOnTheScreen();
  await fireEvent.press(screen.getByTestId(backlogMoveButtonId('SHIP-906')));
  await fireEvent.press(await screen.findByTestId(moveOptionId('other')));
  await fireEvent.press(screen.getByTestId(optionId(moveSideId, 'above')));
  await fireEvent.press(screen.getByTestId(movePickId('SHIP-907')));
}

describe('Screens journeys', () => {
  beforeEach(() => {
    // The runner keeps its outbox in the key-value store: start each journey empty.
    kvValues.clear();
    jest.useFakeTimers({ doNotFake: ['nextTick', 'queueMicrotask', 'setImmediate'] });
  });
  afterEach(() => jest.useRealTimers());

  it('moves SHIP-906 above SHIP-907 in the Backlog', async () => {
    const { api, order, bulk } = backlogJourneyApi();
    const ship907 = order.find((r) => r.key === 'SHIP-907')!;
    // 1. Move → Above or below → above SHIP-907: the preview lists the rows that shift.
    await applyMoveAboveShip907(api);
    expect(screen.getByTestId(movePreviewPosId('SHIP-906'))).toHaveTextContent('4 → 2');
    expect(screen.getByTestId(movePreviewPosId('SHIP-907'))).toHaveTextContent('2 → 3');
    expect(screen.getByTestId(movePreviewPosId('SHIP-892'))).toHaveTextContent('3 → 4');

    // 2. Apply: held 5 s, then one position-only bulk call placing it before SHIP-907.
    await fireEvent.press(screen.getByTestId(moveApplyId));
    await advance(UNDO.tap.ms - 1);
    expect(api.callsTo(bulk)).toHaveLength(0);
    await advance(1);
    await waitFor(() => expect(api.callsTo(bulk)).toHaveLength(1));
    const [item] = (api.callsTo(bulk)[0]!.body as { items: Record<string, unknown>[] }).items;
    expect(item).toMatchObject({
      id: order.find((r) => r.key === 'SHIP-906')!.id,
      changes: null,
      placement: { slot: 'before', beforeItemId: ship907.id },
    });

    // 3. Move it again and Undo inside the window: nothing more is sent.
    await fireEvent.press(screen.getByTestId(backlogMoveButtonId('SHIP-906')));
    await fireEvent.press(await screen.findByTestId(moveOptionId('top')));
    await fireEvent.press(screen.getByTestId(moveApplyId));
    await advance(2000);
    await fireEvent.press(await screen.findByTestId(primitiveIds.toastAction));
    await advance(10_000);
    expect(api.callsTo(bulk)).toHaveLength(1);
  });

  it('sets SHIP-906 blocked on the Features tab and marks the digest seen', async () => {
    const overview = designOverview();
    const row = overview.groups.flatMap((g) => g.rows).find((r) => r.key === 'SHIP-906')!;
    const api = featuresApi({}, { projectId: PROJECT, overview });
    const route = {
      overview: `GET /api/projects/${PROJECT}/features/overview`,
      patch: `PATCH /api/work-items/${row.id}`,
      seen: `POST /api/projects/${PROJECT}/features/digest/seen`,
    };
    await openTab(api, 'features');

    // 1. Expand SHIP-906 and pick Blocked (it is At risk already, and a tap on the current value does nothing).
    await fireEvent.press(
      within(await screen.findByTestId(featureRowId('SHIP-906'))).getByRole('button'),
    );
    await fireEvent.press(await screen.findByTestId(featureHealthButtonId('SHIP-906')));
    await fireEvent.press(await screen.findByTestId(featureHealthOptionId('blocked')));
    await advance(UNDO.tap.ms - 1);
    expect(api.callsTo(route.patch)).toHaveLength(0);
    await advance(1);
    await waitFor(() => expect(api.callsTo(route.patch)).toHaveLength(1));
    expect(api.callsTo(route.patch)[0]!.body).toMatchObject({
      expectedRevision: row.revision,
      changes: { health: { value: 'blocked' } },
    });

    // 2. The since banner opens the digest; Mark as seen sends one seen, and the tab reads the overview again.
    await fireEvent.press(await screen.findByTestId(`${featuresIds.sinceBanner}-action`));
    expect(await screen.findByTestId(screenIds.digest)).toBeOnTheScreen();
    await screen.findByTestId(featureDigestItemId('SHIP-906'));
    const reads = api.callsTo(route.overview).length;
    await fireEvent.press(screen.getByTestId(featureDigestIds.seenTop));
    await waitFor(() => expect(api.callsTo(route.seen)).toHaveLength(1));
    expect(await screen.findByTestId(screenIds.features)).toBeOnTheScreen();
    await waitFor(() => expect(api.callsTo(route.overview).length).toBeGreaterThan(reads));
  });

  it('plus-ones a retro point and makes another an action item for Priya', async () => {
    const retro = designRetro();
    const pointOf = (text: string) =>
      retro.categories
        .flatMap((c) => c.groups.flatMap((g) => g.points))
        .find((p) => p.text === text)!;
    const plusOne = pointOf('Two agents edited the same file.');
    const decide = pointOf('The S22 build took 40 minutes.');
    const priya = aMember({
      user: { ...aMember().user, id: 'user-priya', name: 'Priya Shah', initials: 'PS' },
    });
    const newAction = { workItemId: '0199f000-0000-7000-8000-0000000b0901', key: 'SHIP-901' };
    // The fake server keeps the +1: the retro read after a send shows who supports the point.
    const meRef = { ...aMe(), initials: 'JD', avatarColour: '#2f7fd8', photoUrl: null };
    const setSupport = (on: boolean) => {
      plusOne.supportedByMe = on;
      plusOne.supporters = on ? [meRef] : [];
      return { status: 204 };
    };
    const support = `/api/retros/points/${plusOne.id}/support`;
    const api = retroApi(
      {
        ...signedInApi(),
        [`GET /api/projects/${PROJECT}/members`]: [priya],
        [`POST ${support}`]: () => setSupport(true),
        [`DELETE ${support}`]: () => setSupport(false),
      },
      { projectId: PROJECT, retro, newAction },
    );
    const create = `POST /api/projects/${PROJECT}/retros/actions`;
    const patch = `PATCH /api/work-items/${newAction.workItemId}`;
    await openTab(api, 'retro');

    // 1. Went badly: +1 is sent at once, and a second tap takes it back.
    // The tab opens on the first category with points to decide (D2): Not in plan in the design. Pick Went badly.
    await fireEvent.press(await screen.findByTestId(retroTileId('wentBadly')));
    await fireEvent.press(await screen.findByTestId(retroPlusId(plusOne.id)));
    await waitFor(() => expect(api.callsTo(`POST ${support}`)).toHaveLength(1));
    await waitFor(() => expect(screen.getByTestId(retroPlusId(plusOne.id))).toBeSelected());
    await fireEvent.press(screen.getByTestId(retroPlusId(plusOne.id)));
    await waitFor(() => expect(api.callsTo(`DELETE ${support}`)).toHaveLength(1));
    expect(api.callsTo(`POST ${support}`)).toHaveLength(1);

    // 2. + Action item on another point, owner Priya: held 5 s, then create, read, assign in that order.
    await fireEvent.press(screen.getByTestId(retroAddActionId(decide.id)));
    await fireEvent.press(await screen.findByTestId(retroDecideOwnerId('user-priya')));
    await fireEvent.press(screen.getByTestId(retroIds.decideSubmit));
    await advance(UNDO.tap.ms - 1);
    expect(api.callsTo(create)).toHaveLength(0);
    await advance(1);
    await waitFor(() => expect(api.callsTo(patch)).toHaveLength(1));
    expect(api.callsTo(create)).toHaveLength(1);
    expect(api.callsTo(create)[0]!.body).toMatchObject({
      title: decide.text,
      fixType: 'guide',
      pointIds: [decide.id],
    });
    expect(api.callsTo(patch)[0]!.body).toMatchObject({
      changes: { assignee: { value: 'user-priya' } },
    });
    const order = api.calls
      .map((c) => `${c.method} ${c.path}`)
      .filter((r) =>
        [create, `GET /api/projects/${PROJECT}/work-items/by-key/SHIP-901`, patch].includes(r),
      );
    expect(order).toEqual([
      create,
      `GET /api/projects/${PROJECT}/work-items/by-key/SHIP-901`,
      patch,
    ]);
  });
});
