import type { BacklogLevel } from '@shiplean/core/api/backlog';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { act, fireEvent, screen, within } from '@testing-library/react-native';
import { links } from '../../links';
import { BacklogScreen } from '../../screens/BacklogScreen';
import { screenIds } from '../../screens/testIds';
import { aProject } from '../../test-support/builders';
import {
  aBacklogRow,
  aBacklogView,
  backlogApi,
  designFeatures,
  designStories,
} from '../../test-support/backlog';
import { apiError, fakeApi, type FakeApi } from '../../test-support/fakeApi';
import { signedInApi } from '../../test-support/signedInApi';
import { renderWithVoice } from '../../test-support/voiceUi';
import { primitiveIds, countId, optionId, uiIds } from '../../ui/testIds';
import {
  backlogAsksId,
  backlogDoneId,
  backlogIds,
  backlogMovedId,
  backlogMoveButtonId,
  backlogNeedsBarId,
  backlogOwnerId,
  backlogParentId,
  backlogPickerOptionId,
  backlogRankId,
  backlogRowId,
  backlogTasksId,
  moveSheetId,
} from '../testIds';
import { HIGHLIGHT_MS } from './useRankMoves';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, dismissTo: jest.fn() }),
}));

const mockVoice = jest.fn();
jest.mock('../../voice/VoiceProvider', () => {
  const actual = jest.requireActual('../../voice/VoiceProvider');
  return {
    ...actual,
    useScreenVoice: (...args: unknown[]) => {
      mockVoice(...args);
      return actual.useScreenVoice(...args);
    },
  };
});

const PROJECT = aProject();
const FEATURES = `GET /api/projects/${PROJECT.id}/backlogs/features`;
const STORIES = `GET /api/projects/${PROJECT.id}/backlog`;

const anEpic = (key: string, title: string) =>
  aBacklogRow({ key, title, type: { id: 'epic', name: 'Epic' } });

type Rows = Partial<Record<'epics' | 'features' | 'stories', BacklogRowDto[]>>;

function makeApi(rows: Rows = {}, table: Parameters<typeof backlogApi>[0] = {}): FakeApi {
  return fakeApi({
    ...signedInApi(),
    ...backlogApi(table, { projectId: PROJECT.id, rows }),
    [`GET /api/projects/${PROJECT.id}/members`]: [],
  });
}

interface Opts {
  level?: BacklogLevel;
  epic?: string;
  feature?: string;
  scheme?: 'light' | 'dark';
}

async function renderBacklog({ scheme, ...link }: Opts = {}) {
  return renderWithVoice(<BacklogScreen projectKey="SHIP" {...link} />, { scheme });
}

// The query string of a call, parsed.
const queryOf = (call: { url: string }) =>
  new URLSearchParams(call.url.slice(call.url.indexOf('?') + 1));
const lastQuery = (api: FakeApi, route: string) => queryOf(api.callsTo(route).at(-1)!);
const rowKeys = () =>
  screen
    .getAllByTestId(/^backlog-row-/)
    .map((row) => row.props.testID.slice('backlog-row-'.length));
const clause = (api: FakeApi, route: string) =>
  JSON.parse(lastQuery(api, route).get('filter')!).clauses[0] as { field: string; value: string };

describe.each(['light', 'dark'] as const)('Backlog tab (%s)', (scheme) => {
  it('renders the features state: seven Features in rank order, three marked as needing me', async () => {
    makeApi();
    await renderBacklog({ scheme });

    expect(await screen.findByTestId(backlogRowId('SHIP-707'))).toBeOnTheScreen();
    expect(screen.getByTestId(screenIds.backlog)).toBeOnTheScreen();
    expect(rowKeys()).toEqual([
      'SHIP-707',
      'SHIP-907',
      'SHIP-892',
      'SHIP-906',
      'SHIP-891',
      'SHIP-246',
      'SHIP-256',
    ]);
    expect(screen.getAllByTestId(/^backlog-rank-/).map((n) => n.props.children)).toEqual([
      1, 2, 3, 4, 5, 6, 7,
    ]);
    const marked = screen.getAllByTestId(/^backlog-needs-bar-/);
    expect(marked.map((bar) => bar.props.testID)).toEqual([
      backlogNeedsBarId('SHIP-907'),
      backlogNeedsBarId('SHIP-906'),
      backlogNeedsBarId('SHIP-246'),
    ]);
    expect(screen.getByTestId(countId(backlogIds.chipNeedsMe))).toHaveTextContent('3');
    expect(screen.getAllByTestId(primitiveIds.needsPill)).toHaveLength(3);
    expect(screen.getByTestId(optionId(backlogIds.segment, 'features'))).toBeSelected();
  });

  it('renders the stories state: owner and Feature on each row', async () => {
    const priya = {
      id: 'u1',
      name: 'Priya',
      initials: 'PR',
      avatarColour: '#1f8f6a',
      photoUrl: null,
    };
    const stories = designStories().map((row, i) => (i < 3 ? { ...row, assignee: priya } : row));
    makeApi({ stories });
    await renderBacklog({ scheme, level: 'stories' });

    expect(await screen.findByTestId(backlogRowId('SHIP-921'))).toBeOnTheScreen();
    expect(rowKeys()).toHaveLength(7);
    for (const row of stories) {
      expect(screen.getByTestId(backlogParentId(row.key))).toHaveTextContent(
        `${row.parent!.key} ${row.parent!.title}`,
      );
    }
    expect(screen.getByTestId(backlogOwnerId('SHIP-921'))).toHaveTextContent('Priya');
    expect(screen.queryByTestId(backlogOwnerId('SHIP-912'))).toBeNull();
  });

  it('renders the needs-me state: only the rows that need me, ranks unchanged', async () => {
    makeApi();
    await renderBacklog({ scheme });
    await screen.findByTestId(backlogRowId('SHIP-707'));

    await fireEvent.press(screen.getByTestId(backlogIds.chipNeedsMe));

    expect(rowKeys()).toEqual(['SHIP-907', 'SHIP-906', 'SHIP-246']);
    expect(screen.getAllByTestId(/^backlog-rank-/).map((n) => n.props.children)).toEqual([2, 4, 6]);
    expect(screen.getByTestId(backlogAsksId('SHIP-907'))).toHaveTextContent('2 decisions');
    expect(screen.getByTestId(backlogAsksId('SHIP-906'))).toHaveTextContent('1 question');
    expect(screen.getByTestId(backlogAsksId('SHIP-246'))).toHaveTextContent(
      '1 decision · 1 question',
    );
    expect(screen.getByTestId(backlogIds.chipNeedsMe)).toBeSelected();
  });

  it("shows a Story's task counts from its rollups", async () => {
    makeApi();
    await renderBacklog({ scheme, level: 'stories' });

    expect(await screen.findByTestId(backlogTasksId('SHIP-921'))).toHaveTextContent('Tasks 3/7');
    expect(screen.getByTestId(backlogTasksId('SHIP-923'))).toHaveTextContent('Tasks 2/3');
    expect(screen.queryByTestId(backlogTasksId('SHIP-912'))).toBeNull();
  });

  it('shows health only for at risk and blocked', async () => {
    const features = designFeatures().map((row) =>
      row.key === 'SHIP-891' ? { ...row, health: 'blocked' as const } : row,
    );
    makeApi({ features });
    await renderBacklog({ scheme });
    await screen.findByTestId(backlogRowId('SHIP-707'));

    const health = (key: string) =>
      within(screen.getByTestId(backlogRowId(key))).queryByTestId(primitiveIds.healthPill);
    expect(health('SHIP-707')).toBeNull();
    expect(health('SHIP-892')).toBeNull();
    expect(health('SHIP-907')).toHaveTextContent('At risk');
    expect(health('SHIP-891')).toHaveTextContent('Blocked');
  });
});

describe('Backlog tab', () => {
  beforeEach(() => {
    mockPush.mockClear();
    mockVoice.mockClear();
  });

  it('renders the applied state: SHIP-906 at rank 2 marked up from 4', async () => {
    const before = designFeatures();
    const byKey = (key: string) => before.find((r) => r.key === key)!;
    const after = [
      'SHIP-707',
      'SHIP-906',
      'SHIP-907',
      'SHIP-892',
      'SHIP-891',
      'SHIP-246',
      'SHIP-256',
    ].map((key, i) => ({ ...byKey(key), rank: i + 1 }));
    let reads = 0;
    makeApi(
      {},
      { [FEATURES]: () => ({ ...aBacklogView('features', reads++ === 0 ? before : after) }) },
    );
    const { queryClient } = await renderBacklog();
    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(screen.queryByTestId(backlogMovedId('SHIP-906'))).toBeNull();

    await act(async () => {
      await queryClient.invalidateQueries();
    });

    expect(await screen.findByTestId(backlogMovedId('SHIP-906'))).toHaveTextContent('↑ from 4');
    expect(screen.getByTestId(backlogRankId('SHIP-906'))).toHaveTextContent('2');
    expect(screen.getByTestId(backlogMovedId('SHIP-907'))).toHaveTextContent('↓ from 2');
    expect(screen.queryByTestId(backlogMovedId('SHIP-707'))).toBeNull();
  });

  it('drops the moved marker after 3 s', async () => {
    jest.useFakeTimers({ doNotFake: ['nextTick', 'queueMicrotask', 'setImmediate'] });
    try {
      const before = designFeatures();
      const after = [before[1]!, before[0]!, ...before.slice(2)].map((r, i) => ({
        ...r,
        rank: i + 1,
      }));
      let reads = 0;
      makeApi({}, { [FEATURES]: () => aBacklogView('features', reads++ === 0 ? before : after) });
      const { queryClient } = await renderBacklog();
      await screen.findByTestId(backlogRowId('SHIP-707'));
      await act(async () => {
        await queryClient.invalidateQueries();
      });
      expect(await screen.findByTestId(backlogMovedId('SHIP-907'))).toBeOnTheScreen();

      await act(async () => {
        jest.advanceTimersByTime(HIGHLIGHT_MS);
      });

      expect(screen.queryByTestId(backlogMovedId('SHIP-907'))).toBeNull();
    } finally {
      jest.useRealTimers();
    }
  });

  it('sends includeCompleted=true when Open is turned off', async () => {
    const api = makeApi();
    await renderBacklog();
    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(lastQuery(api, FEATURES).get('includeCompleted')).toBe('false');
    expect(lastQuery(api, FEATURES).get('mode')).toBeNull();

    await fireEvent.press(screen.getByTestId(backlogIds.chipOpen));

    expect(screen.getByTestId(backlogIds.chipOpen)).not.toBeSelected();
    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(lastQuery(api, FEATURES).get('includeCompleted')).toBe('true');
  });

  it('sends the epic criteria for the Epic chip and the parent criteria for the Feature chip', async () => {
    const epic = anEpic('SHIP-3', 'Mobile');
    const features = designFeatures();
    const api = makeApi({ epics: [epic], features });
    await renderBacklog();
    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(lastQuery(api, FEATURES).get('filter')).toBe('');

    await fireEvent.press(screen.getByTestId(backlogIds.chipParent));
    await fireEvent.press(await screen.findByTestId(backlogPickerOptionId('SHIP-3')));

    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(clause(api, FEATURES)).toMatchObject({ field: 'epic', value: epic.id });
    expect(screen.getByTestId(backlogIds.chipParent)).toHaveTextContent('SHIP-3');

    await fireEvent.press(screen.getByTestId(optionId(backlogIds.segment, 'stories')));
    expect(screen.getByTestId(backlogIds.chipParent)).toHaveTextContent('All features');
    await fireEvent.press(screen.getByTestId(backlogIds.chipParent));
    await fireEvent.press(await screen.findByTestId(backlogPickerOptionId('SHIP-906')));

    await screen.findByTestId(backlogRowId('SHIP-921'));
    expect(clause(api, STORIES)).toMatchObject({
      field: 'parent',
      value: features.find((f) => f.key === 'SHIP-906')!.id,
    });
  });

  it('opens at Stories filtered to a Feature, and at Features filtered to an Epic, from the link params', async () => {
    const epic = anEpic('SHIP-3', 'Mobile');
    const features = designFeatures();
    const api = makeApi({ epics: [epic], features });
    const first = await renderBacklog({ feature: 'SHIP-906' });

    expect(await screen.findByTestId(backlogRowId('SHIP-921'))).toBeOnTheScreen();
    expect(screen.getByTestId(optionId(backlogIds.segment, 'stories'))).toBeSelected();
    expect(clause(api, STORIES)).toMatchObject({
      field: 'parent',
      value: features.find((f) => f.key === 'SHIP-906')!.id,
    });
    expect(api.callsTo(STORIES).every((c) => queryOf(c).get('filter') !== '')).toBe(true);
    await first.unmount();
    const featureReads = api.callsTo(FEATURES).length;

    await renderBacklog({ level: 'features', epic: 'SHIP-3' });

    expect(await screen.findByTestId(backlogRowId('SHIP-707'))).toBeOnTheScreen();
    expect(clause(api, FEATURES)).toMatchObject({ field: 'epic', value: epic.id });
    expect(
      api
        .callsTo(FEATURES)
        .slice(featureReads)
        .every((c) => queryOf(c).get('filter') !== ''),
    ).toBe(true);
    expect(screen.getByTestId(backlogIds.chipParent)).toHaveTextContent('SHIP-3');
  });

  it('shows Nothing to show when the level has no rows', async () => {
    makeApi({ features: [] });
    await renderBacklog();

    expect(await screen.findByTestId(uiIds.emptyState)).toHaveTextContent('Nothing to show');
  });

  it('shows Nothing matches this filter when a chip leaves no rows', async () => {
    makeApi();
    await renderBacklog({ level: 'stories' });
    await screen.findByTestId(backlogRowId('SHIP-921'));

    await fireEvent.press(screen.getByTestId(backlogIds.chipNeedsMe));

    expect(screen.getByTestId(uiIds.emptyState)).toHaveTextContent('Nothing matches this filter');
  });

  it("shows the error with Retry and no server text for a failure that isn't a 400", async () => {
    let failing = true;
    const api = makeApi(
      {},
      {
        [FEATURES]: () =>
          failing ? apiError(500, 'internal_error') : aBacklogView('features', designFeatures()),
      },
    );
    await renderBacklog();

    const error = await screen.findByTestId(uiIds.errorState);
    expect(within(error).getByText("Couldn't load the backlog.")).toBeOnTheScreen();
    expect(within(error).queryByText(/Failed/)).toBeNull();
    failing = false;
    await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));

    expect(await screen.findByTestId(backlogRowId('SHIP-707'))).toBeOnTheScreen();
    expect(api.callsTo(FEATURES)).toHaveLength(2);
  });

  it("shows the server's message for a 400", async () => {
    makeApi({}, { [FEATURES]: () => apiError(400, 'validation', 'The filter is not valid.') });
    await renderBacklog();

    const error = await screen.findByTestId(uiIds.errorState);
    expect(within(error).getByText("Couldn't load the backlog.")).toBeOnTheScreen();
    expect(within(error).getByText('The filter is not valid.')).toBeOnTheScreen();
  });

  it('opens the Move sheet stub for the row', async () => {
    makeApi();
    await renderBacklog();
    await screen.findByTestId(backlogRowId('SHIP-906'));
    expect(screen.queryByTestId(moveSheetId)).toBeNull();

    await fireEvent.press(screen.getByTestId(backlogMoveButtonId('SHIP-906')));

    expect(screen.getByTestId(moveSheetId)).toBeOnTheScreen();
  });

  it('offers no Move button while an Epic or Feature filter narrows the list', async () => {
    makeApi({ epics: [anEpic('SHIP-3', 'Mobile')] });
    await renderBacklog({ epic: 'SHIP-3' });

    await screen.findByTestId(backlogRowId('SHIP-707'));

    expect(screen.queryAllByTestId(/^backlog-move-/)).toHaveLength(0);
    expect(screen.getByLabelText('Add under SHIP-3')).toBeOnTheScreen();
  });

  it('opens the item when a row is tapped', async () => {
    makeApi();
    await renderBacklog();

    await screen.findByTestId(backlogRowId('SHIP-906'));
    await fireEvent.press(
      screen.getByText('Voice commands on the mobile app with on-device models'),
    );

    expect(mockPush).toHaveBeenCalledWith(links.item('SHIP', 'SHIP-906'));
  });

  it('pull down refetches the list', async () => {
    const api = makeApi();
    await renderBacklog();
    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(api.callsTo(FEATURES)).toHaveLength(1);

    await act(async () => {
      screen.getByTestId(backlogIds.list).props.refreshControl.props.onRefresh();
    });

    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(api.callsTo(FEATURES)).toHaveLength(2);
  });

  it('shows Epics with their Feature counts and no parent chip or add row', async () => {
    const epic = {
      ...anEpic('SHIP-3', 'Mobile'),
      rollups: [
        { type: 'feature' as const, open: 3, closed: 2, inCurrentSprint: 0, inOtherSprint: 0 },
      ],
    };
    makeApi({ epics: [epic] });
    await renderBacklog({ level: 'epics' });

    await screen.findByTestId(backlogRowId('SHIP-3'));
    expect(screen.getByTestId(backlogDoneId('SHIP-3'))).toHaveTextContent('2 of 5 features done');
    expect(screen.queryByTestId(backlogIds.chipParent)).toBeNull();
    expect(screen.queryByLabelText(/^Add/)).toBeNull();
  });

  it('applies a screen view from voice: switches the level and turns on the Epic chip', async () => {
    const epic = anEpic('SHIP-3', 'Mobile');
    const api = makeApi({ epics: [epic] });
    await renderBacklog({ level: 'stories' });
    await screen.findByTestId(backlogRowId('SHIP-921'));
    const onView = () => mockVoice.mock.calls.at(-1)![1].onView as (v: unknown) => void;

    await act(async () => {
      onView()({ kind: 'screen', command: 'level', args: { level: 'features' } });
    });
    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(screen.getByTestId(optionId(backlogIds.segment, 'features'))).toBeSelected();

    await act(async () => {
      onView()({ kind: 'screen', command: 'filter', args: { chip: 'epic', key: 'SHIP-3' } });
    });
    await screen.findByTestId(backlogRowId('SHIP-707'));
    expect(clause(api, FEATURES)).toMatchObject({ field: 'epic', value: epic.id });
    expect(screen.getByTestId(backlogIds.chipParent)).toHaveTextContent('SHIP-3');

    await act(async () => {
      onView()({ kind: 'screen', command: 'filter', args: { chip: 'needsMe' } });
    });
    expect(screen.getByTestId(backlogIds.chipNeedsMe)).toBeSelected();
  });
});
