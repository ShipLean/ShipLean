import type { ChildSummaryDto, WorkItemDto } from '@shiplean/core/api/workItems';
import type { MemberDto } from '@shiplean/core/api/projects';
import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import members from '../../../../ShipLean.TestSupport/DtoFixtures/projects/members.json';
import { links } from '../../links';
import { aProject } from '../../test-support/builders';
import { FEATURE_STATES } from '../../test-support/features';
import { apiError } from '../../test-support/fakeApi';
import { fakeRunner } from '../../test-support/actions';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { aFeatureItem, workItemApi } from '../../test-support/workItem';
import type { Scheme } from '../../theme';
import { uiIds } from '../../ui/testIds';
import { ItemActionsRow } from '../actionsRow/ItemActionsRow';
import * as ids from '../testIds';
import { WorkItemActionsProvider } from '../WorkItemActions';
import { AssignSheet } from './AssignSheet';
import { ChangeStateSheet } from './ChangeStateSheet';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, dismissTo: jest.fn() }),
}));

const PROJECT = aProject().id;
const STATES_ROUTE = `GET /api/projects/${PROJECT}/states`;
const MEMBERS_ROUTE = `GET /api/projects/${PROJECT}/members`;
const [proposed, active] = FEATURE_STATES;
const ref = (s: (typeof FEATURE_STATES)[number]) => ({
  id: s.id,
  name: s.name,
  category: s.category,
});

const child = (type: 'story' | 'feature', n: number): ChildSummaryDto => ({
  id: `child-${type}-${n}`,
  key: `SHIP-${1000 + n}`,
  type: { id: type, name: type },
  title: `Child ${n}`,
  state: ref(proposed),
  assignee: null,
  points: null,
});

const feature = (overrides: Partial<WorkItemDto> = {}) =>
  aFeatureItem({
    projectId: PROJECT,
    revision: 7,
    state: ref(active),
    children: [1, 2, 3, 4, 5, 6].map((n) => child('story', n)),
    ...overrides,
  });

const open = async (
  item: WorkItemDto,
  scheme: Scheme,
  table: Parameters<typeof workItemApi>[0] = {},
) => {
  // The states come back out of order: the sheet sorts them.
  const shuffled = [...FEATURE_STATES].reverse();
  const fake = workItemApi(
    { [STATES_ROUTE]: shuffled, ...table },
    { projectId: PROJECT, items: [item] },
  );
  const runner = fakeRunner();
  const { queryClient } = await renderWithRunner(
    <WorkItemActionsProvider>
      <ItemActionsRow item={item} projectKey="SHIP" />
    </WorkItemActionsProvider>,
    { runner, scheme },
  );
  // The sheets read the states and members as the row mounts: let both settle before the test acts.
  await waitFor(() => expect(queryClient.isFetching()).toBe(0));
  return { fake, runner };
};

beforeEach(() => mockPush.mockClear());

describe.each<Scheme>(['light', 'dark'])('Work item sheets (%s)', (scheme) => {
  it('lists every state of the item type in order and dispatches setState', async () => {
    const item = feature();
    const { runner } = await open(item, scheme);
    await fireEvent.press(screen.getByTestId(ids.changeStateButtonId));
    await screen.findByTestId(ids.stateOptionId(proposed.name));
    const shown = FEATURE_STATES.map((s) => screen.getByTestId(ids.stateOptionId(s.name)));
    // In order: the option ids are found top to bottom.
    const flat = JSON.stringify(screen.toJSON());
    const at = FEATURE_STATES.map((s) => flat.indexOf(ids.stateOptionId(s.name)));
    expect(at).toEqual([...at].sort((a, b) => a - b));
    expect(shown).toHaveLength(FEATURE_STATES.length);
    // The current one is ticked.
    expect(
      screen.getByTestId(ids.stateOptionId(active.name)).props.accessibilityState.selected,
    ).toBe(true);

    await fireEvent.press(screen.getByTestId(ids.stateOptionId(proposed.name)));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([
      {
        area: 'workItem',
        projectId: PROJECT,
        kind: 'setState',
        itemId: item.id,
        key: item.key,
        revision: 7,
        stateId: proposed.id,
        stateName: proposed.name,
        fromStateId: active.id,
        fromStateName: active.name,
      },
    ]);
  });

  it('lists members and No owner and dispatches assign', async () => {
    const item = feature();
    const { runner, fake } = await open(item, scheme);
    await fireEvent.press(screen.getByTestId(ids.assignButtonId));
    await screen.findByTestId(ids.noOwnerOptionId);
    const members = fake.callsTo(MEMBERS_ROUTE);
    expect(members.length).toBeGreaterThan(0);
    const people = screen
      .getAllByTestId(/^wi-owner-option-(?!none)/)
      .map((n) => n.props.testID as string);
    expect(people).toHaveLength(2);

    await fireEvent.press(screen.getByTestId(people[0]));
    await fireEvent.press(screen.getByTestId(ids.assignButtonId));
    await fireEvent.press(await screen.findByTestId(ids.noOwnerOptionId));
    const [first, second] = runner.dispatched.map((d) => d.batch[0]);
    expect(first).toMatchObject({
      kind: 'assign',
      projectId: PROJECT,
      itemId: item.id,
      revision: 7,
      userId: people[0].replace('wi-owner-option-', ''),
      fromUserId: item.assignee?.id ?? null,
    });
    expect(second).toMatchObject({ kind: 'assign', userId: null, name: null });
  });

  it('leaves disabled members out of the assign list', async () => {
    const [joel, priya] = members as MemberDto[];
    await open(feature(), scheme, {
      [MEMBERS_ROUTE]: [joel, { ...priya, status: 'disabled' }],
    });
    await fireEvent.press(screen.getByTestId(ids.assignButtonId));
    await screen.findByTestId(ids.noOwnerOptionId);
    expect(screen.getByTestId(ids.ownerOptionId(joel.user.id))).toBeTruthy();
    expect(screen.queryByTestId(ids.ownerOptionId(priya.user.id))).toBeNull();
  });

  it('opens the Backlog at Stories filtered to the Feature from 6 stories', async () => {
    const item = feature();
    await open(item, scheme);
    expect(screen.getByText('6 stories ›')).toBeTruthy();
    await fireEvent.press(screen.getByTestId(ids.childrenLinkId));
    expect(mockPush).toHaveBeenCalledWith(
      links.backlog('SHIP', { level: 'stories', feature: item.key }),
    );
  });

  it('opens the Backlog at Features filtered to the Epic from an Epic', async () => {
    const item = feature({
      key: 'SHIP-900',
      type: { id: 'epic', name: 'Epic' },
      children: [1, 2].map((n) => child('feature', n)),
    });
    await open(item, scheme);
    expect(screen.getByText('2 features ›')).toBeTruthy();
    await fireEvent.press(screen.getByTestId(ids.childrenLinkId));
    expect(mockPush).toHaveBeenCalledWith(
      links.backlog('SHIP', { level: 'features', epic: 'SHIP-900' }),
    );
  });

  it('shows the states read error with Retry in the sheet', async () => {
    const { fake } = await open(feature(), scheme, {
      [STATES_ROUTE]: () => apiError(500, 'server', 'The states could not be read.'),
    });
    await fireEvent.press(screen.getByTestId(ids.changeStateButtonId));
    await screen.findByTestId(ids.stateSheetErrorId);
    const before = fake.callsTo(STATES_ROUTE).length;
    await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));
    await waitFor(() => expect(fake.callsTo(STATES_ROUTE).length).toBe(before + 1));
  });

  it('does not offer the children link on a Story', async () => {
    await open(feature({ type: { id: 'story', name: 'Story' } }), scheme);
    expect(screen.queryByTestId(ids.childrenLinkId)).toBeNull();
  });
});

// Keeps the standalone sheets exercised with their own props, closed.
describe('Work item sheets closed', () => {
  it('render nothing until opened', async () => {
    const item = feature();
    workItemApi({}, { projectId: PROJECT, items: [item] });
    const { queryClient } = await renderWithRunner(
      <WorkItemActionsProvider>
        <ChangeStateSheet item={item} open={false} onClose={jest.fn()} />
        <AssignSheet item={item} open={false} onClose={jest.fn()} />
      </WorkItemActionsProvider>,
    );
    await waitFor(() => expect(queryClient.isFetching()).toBe(0));
    expect(screen.queryByTestId(ids.changeStateSheetId)).toBeNull();
    expect(screen.queryByTestId(ids.assignSheetId)).toBeNull();
  });
});
