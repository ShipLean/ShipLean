import type { StateCategory } from '@shiplean/core/api/refs';
import type { FeatureBoardRowDto, FeatureBoardViewDto } from '@shiplean/core/api/views';
import type { FeaturesOverviewDto } from '@shiplean/core/api/featuresOverview';
import { platform } from '@shiplean/core/platform';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { openBrowserAsync } from 'expo-web-browser';
import { links } from '../../links';
import { FeatureBoardScreen } from '../../screens/FeatureBoardScreen';
import { screenIds } from '../../screens/testIds';
import { aProject, aWorkItem } from '../../test-support/builders';
import {
  aFeatureGroup,
  aFeatureRow,
  anOverview,
  designBoard,
  designOverview,
  featuresApi,
} from '../../test-support/features';
import { apiError, type ApiTable } from '../../test-support/fakeApi';
import { anInbox } from '../../test-support/inbox';
import { renderWithVoice } from '../../test-support/voiceUi';
import type { Scheme } from '../../theme';
import { primitiveIds, uiIds } from '../../ui/testIds';
import {
  featureBoardGroupCountId,
  featureBoardGroupId,
  featureBoardIds,
  featureBoardOwnerId,
  featureBoardSectionEmptyId,
  featureBoardSprintId,
  featureBoardStoryId,
  featureBoardTaskBarId,
  featureBoardTaskDoingId,
  featureBoardTaskDoneId,
  featureBoardTaskTextId,
} from '../testIds';

const mockPush = jest.fn();
const mockDismissTo = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, dismissTo: mockDismissTo }),
}));

const mockSpec = jest.fn();
jest.mock('../voiceSpec', () => {
  const actual = jest.requireActual('../voiceSpec');
  return {
    ...actual,
    featuresVoiceSpec: (ctx: unknown) => {
      mockSpec(ctx);
      return actual.featuresVoiceSpec(ctx);
    },
  };
});

const PROJECT = aProject();
const project = `/api/projects/${PROJECT.id}`;
const KEY = 'SHIP-906';
const BOARD_ROUTE = `GET ${project}/work-items/by-key/${KEY}/feature-board`;
const ITEM_ROUTE = `GET ${project}/work-items/by-key/${KEY}`;

// A story of the fixture's first row, with its own key, state and type, in the board's own section.
function aStory(
  base: FeatureBoardRowDto,
  key: string,
  category: StateCategory,
  type: 'story' | 'bug' = 'story',
): FeatureBoardRowDto {
  const row = structuredClone(base);
  row.needsYou = null;
  row.parent.id = `id-${key}`;
  row.parent.key = key;
  row.parent.title = `Title of ${key}`;
  row.parent.type = { id: type, name: type === 'bug' ? 'Bug' : 'User Story' };
  row.parent.state = { ...row.parent.state, category };
  return row;
}

// A Feature with no Features under it: one section, so one plain list, with a story in every state.
function leafBoard(): FeatureBoardViewDto {
  const board = designBoard();
  board.sections = [board.sections[0]];
  board.rows = board.rows.map((row) => ({ ...row, featureId: board.sections[0].id }));
  board.rows.push(
    aStory(board.rows[0], 'SHIP-190', 'resolved'),
    aStory(board.rows[0], 'SHIP-191', 'proposed', 'bug'),
  );
  board.rows[board.rows.length - 1].cells = [];
  return board;
}

const SHIP_906_ITEM = aWorkItem({
  key: KEY,
  health: 'atRisk',
  statusLine: 'Parakeet runs at 1.4x real time on the S22.',
  statusVerified: {
    at: '2030-01-01T09:00:00+00:00',
    atLabel: '4 days old',
    stale: true,
    by: null,
    agent: 'Claude Code',
    via: null,
  },
});

interface Opts {
  board?: FeatureBoardViewDto;
  overview?: FeaturesOverviewDto;
  table?: ApiTable;
  scheme?: Scheme;
}

async function renderBoard({ board = leafBoard(), overview, table = {}, scheme }: Opts = {}) {
  const api = featuresApi(
    {
      [`GET ${project}/inbox`]: anInbox({ projectId: PROJECT.id, items: [] }),
      [`GET ${project}/members`]: [],
      [ITEM_ROUTE]: SHIP_906_ITEM,
      [BOARD_ROUTE]: board,
      ...table,
    },
    { projectId: PROJECT.id, board, overview, boardKeys: [KEY] },
  );
  const result = await renderWithVoice(<FeatureBoardScreen projectKey="SHIP" featureKey={KEY} />, {
    scheme,
  });
  return { api, ...result };
}

const text = (id: string) => screen.getByTestId(id);

beforeEach(() => {
  mockPush.mockClear();
  mockDismissTo.mockClear();
  mockSpec.mockClear();
  jest.mocked(openBrowserAsync).mockClear();
});

describe.each(['light', 'dark'] as const)('One Feature board (%s)', (scheme) => {
  it('renders the feature state: header, four figures, the read-only link and stories grouped by state', async () => {
    await renderBoard({ scheme });

    expect(await screen.findByTestId(featureBoardStoryId('SHIP-141'))).toBeOnTheScreen();
    expect(screen.getByTestId(screenIds.featureBoard)).toBeOnTheScreen();
    expect(text(featureBoardIds.key)).toHaveTextContent('SHIP-906');
    expect(text(featureBoardIds.title)).toHaveTextContent('Voice commands with on-device models');
    const header = within(text(featureBoardIds.header));
    expect(header.getAllByTestId(primitiveIds.statePill)[0]).toHaveTextContent('Active');
    await waitFor(() => expect(header.getByTestId(primitiveIds.healthPill)).toBeOnTheScreen());
    expect(header.getByTestId(primitiveIds.healthPill)).toHaveTextContent('At risk');
    expect(text(featureBoardIds.status)).toHaveTextContent(
      'Parakeet runs at 1.4x real time on the S22.',
    );
    expect(text(featureBoardIds.statusBy)).toHaveTextContent('Claude Code (agent) · 4 days old');
    expect(text(featureBoardIds.statusStale)).toHaveTextContent('Stale');
    // Stories and bugs are counted apart from the rows: the fixture's header counts both together.
    expect(text(featureBoardIds.figureStories)).toHaveTextContent('1/5');
    expect(text(featureBoardIds.figureBugs)).toHaveTextContent('1/2');
    expect(text(featureBoardIds.figureTasks)).toHaveTextContent('6/11');
    expect(text(featureBoardIds.figurePoints)).toHaveTextContent('5/21');
    expect(text(featureBoardIds.webLink)).toBeOnTheScreen();
    expect(screen.getByText('Read-only. Tasks move on the web.')).toBeOnTheScreen();

    const groups = screen.getAllByTestId(/^feature-board-group-all-[a-zA-Z]+$/);
    expect(groups.map((g) => g.props.testID)).toEqual([
      featureBoardGroupId('all', 'inProgress'),
      featureBoardGroupId('all', 'resolved'),
      featureBoardGroupId('all', 'proposed'),
      featureBoardGroupId('all', 'completed'),
    ]);
    const keys = screen
      .getAllByTestId(/^feature-board-story-/)
      .map((n) => n.props.testID.slice('feature-board-story-'.length));
    expect(keys).toEqual(['SHIP-141', 'SHIP-144', 'SHIP-129', 'SHIP-190', 'SHIP-191']);
    expect(text(featureBoardGroupCountId('all', 'inProgress'))).toHaveTextContent('3');
    expect(text(featureBoardGroupCountId('all', 'completed'))).toHaveTextContent('Closed · 2');
    expect(screen.queryByTestId(featureBoardStoryId('SHIP-138'))).toBeNull();
    expect(text(featureBoardSprintId('SHIP-141'))).toHaveTextContent('Sprint 24');
    expect(text(featureBoardSprintId('SHIP-191'))).toHaveTextContent('Sprint 24');
    expect(text(featureBoardOwnerId('SHIP-141'))).toHaveTextContent('Priya Shah');
  });

  it('shows a task bar per story and never a task card', async () => {
    await renderBoard({ scheme });

    await screen.findByTestId(featureBoardStoryId('SHIP-141'));
    // SHIP-141: a New, an Active and a Resolved task. SHIP-129: one each New and Active, two Closed.
    expect(text(featureBoardTaskTextId('SHIP-141'))).toHaveTextContent(
      '0 done · 2 doing · 1 to do',
    );
    expect(text(featureBoardTaskTextId('SHIP-129'))).toHaveTextContent(
      '2 done · 1 doing · 1 to do',
    );
    expect(text(featureBoardTaskBarId('SHIP-141'))).toBeOnTheScreen();
    expect(screen.queryByTestId(featureBoardTaskDoneId('SHIP-141'))).toBeNull();
    expect(text(featureBoardTaskDoingId('SHIP-141'))).toBeOnTheScreen();
    expect(text(featureBoardTaskDoneId('SHIP-129'))).toBeOnTheScreen();
    expect(text(featureBoardTaskTextId('SHIP-191'))).toHaveTextContent('No tasks yet');
    // The tasks' titles and keys appear nowhere.
    expect(screen.queryByText('E2E test')).toBeNull();
    expect(screen.queryByText('UI: saved card picker')).toBeNull();
    expect(screen.queryByText('SHIP-173')).toBeNull();
  });

  it('shows the Needs you pill from the board row', async () => {
    const { api } = await renderBoard({ scheme });

    const waiting = await screen.findByTestId(featureBoardStoryId('SHIP-141'));
    expect(within(waiting).getByTestId(primitiveIds.needsPill)).toBeOnTheScreen();
    expect(
      within(screen.getByTestId(featureBoardStoryId('SHIP-144'))).queryByTestId(
        primitiveIds.needsPill,
      ),
    ).toBeNull();
    expect(screen.getAllByTestId(primitiveIds.needsPill)).toHaveLength(1);
    expect(api.calls.filter((c) => c.path.includes('/backlog'))).toEqual([]);
  });

  it('titles nested Feature sections depth-first', async () => {
    await renderBoard({ board: designBoard(), scheme });

    await screen.findByTestId(featureBoardStoryId('SHIP-141'));
    const sections = screen.getAllByTestId(/^feature-board-section-SHIP-\d+$/);
    expect(sections.map((s) => within(s).getAllByRole('header')[0])).toHaveLength(3);
    expect(sections.map((s) => within(s).getAllByRole('header')[0].props.children)).toEqual([
      'Directly under SHIP-101',
      'SHIP-110 Saved cards',
      'SHIP-111 Card vault',
    ]);
    // Each section holds its own stories, grouped by state.
    expect(
      within(sections[0])
        .getAllByTestId(/^feature-board-story-/)
        .map((n) => n.props.testID),
    ).toEqual(['feature-board-story-SHIP-141', 'feature-board-story-SHIP-144']);
    expect(within(sections[1]).getByTestId(featureBoardStoryId('SHIP-129'))).toBeOnTheScreen();
    expect(within(sections[2]).queryByTestId(featureBoardStoryId('SHIP-129'))).toBeNull();
    expect(screen.getByTestId(featureBoardGroupId('SHIP-111', 'completed'))).toBeOnTheScreen();
  });

  it('opens the web board from the read-only link', async () => {
    await renderBoard({ scheme });

    await fireEvent.press(await screen.findByTestId(featureBoardIds.webLink));

    const origin = platform().apiOrigin();
    expect(origin).not.toBe('');
    expect(openBrowserAsync).toHaveBeenCalledWith(`${origin}/p/SHIP/items/${KEY}/board`);
  });

  it('opens a story from its row and goes back to the Features tab', async () => {
    await renderBoard({ scheme });

    await fireEvent.press(await screen.findByTestId(featureBoardStoryId('SHIP-144')));
    expect(mockPush).toHaveBeenCalledWith(links.item('SHIP', 'SHIP-144'));
    await fireEvent.press(text(featureBoardIds.back));
    expect(mockDismissTo).toHaveBeenCalledWith(links.features('SHIP'));
  });

  it('draws the bar from the overview roll-ups, or the points percent for a folded Feature', async () => {
    await renderBoard({ scheme });
    await screen.findByTestId(featureBoardStoryId('SHIP-141'));
    await waitFor(() => expect(screen.getByTestId(primitiveIds.progressBar)).toBeOnTheScreen());
    expect(screen.queryByTestId(featureBoardIds.percent)).toBeNull();
  });

  it('draws the points percent when the overview has no row for the Feature', async () => {
    const overview = anOverview([
      aFeatureGroup('inProgress', [aFeatureRow({ key: 'SHIP-1' })]),
      aFeatureGroup('completed', [aFeatureRow({ key: KEY })]),
    ]);
    await renderBoard({ overview, scheme });

    const percent = await screen.findByTestId(featureBoardIds.percent);
    // 5 of 21 points: the web header's Math.round(5 / 21 * 100).
    expect(percent.props.accessibilityValue).toEqual({ min: 0, max: 100, now: 24 });
    expect(screen.queryByTestId(primitiveIds.progressBar)).toBeNull();
  });

  it('shows the empty, error and not-found states: empty', async () => {
    const board = leafBoard();
    board.rows = [];
    await renderBoard({ board, scheme });

    expect(await screen.findByText('No stories or bugs yet.')).toBeOnTheScreen();
    expect(screen.getByTestId(uiIds.emptyState)).toBeOnTheScreen();
    expect(screen.queryByTestId(featureBoardSectionEmptyId('SHIP-101'))).toBeNull();
  });

  it('passes the project and board: true to the voice spec, focused on this Feature', async () => {
    await renderBoard({ overview: designOverview(), scheme });

    await waitFor(() =>
      expect(mockSpec).toHaveBeenLastCalledWith(
        expect.objectContaining({
          projectId: PROJECT.id,
          board: true,
          projectKey: 'SHIP',
          focus: expect.objectContaining({ key: KEY }),
        }),
      ),
    );
  });

  it('pull down refetches every read', async () => {
    const { api } = await renderBoard({ scheme });
    await screen.findByTestId(featureBoardStoryId('SHIP-141'));
    await waitFor(() => expect(api.callsTo(ITEM_ROUTE)).toHaveLength(1));
    const reads = [
      BOARD_ROUTE,
      ITEM_ROUTE,
      `GET ${project}/features/overview`,
      `GET ${project}/inbox`,
    ];
    const before = reads.map((route) => api.callsTo(route).length);

    await act(async () => {
      await screen.getByTestId(featureBoardIds.scroll).props.refreshControl.props.onRefresh();
    });

    expect(reads.map((route) => api.callsTo(route).length)).toEqual(before.map((n) => n + 1));
  });
});

describe('One Feature board states', () => {
  it('shows the empty, error and not-found states: error with Retry', async () => {
    const board = leafBoard();
    const api = featuresApi(
      {
        [`GET ${project}/inbox`]: anInbox({ projectId: PROJECT.id, items: [] }),
        [`GET ${project}/members`]: [],
        [ITEM_ROUTE]: SHIP_906_ITEM,
      },
      { projectId: PROJECT.id, board, boardKeys: [KEY] },
    );
    api.failNext(BOARD_ROUTE, apiError(500, 'internal'));
    await renderWithVoice(<FeatureBoardScreen projectKey="SHIP" featureKey={KEY} />);

    expect(await screen.findByText("Couldn't load the board.")).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));
    expect(await screen.findByTestId(featureBoardStoryId('SHIP-141'))).toBeOnTheScreen();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
  });

  it('shows the empty, error and not-found states: not found', async () => {
    await renderBoard({ table: { [BOARD_ROUTE]: apiError(404, 'not_found') } });

    expect(await screen.findByText("SHIP-906 isn't in this project.")).toBeOnTheScreen();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
  });

  it('sends no read with an empty project id on a cold open', async () => {
    const board = leafBoard();
    const api = featuresApi(
      {
        [`GET ${project}/inbox`]: anInbox({ projectId: PROJECT.id, items: [] }),
        [`GET ${project}/members`]: [],
        [ITEM_ROUTE]: SHIP_906_ITEM,
      },
      { projectId: PROJECT.id, board, boardKeys: [KEY] },
    );
    api.hang('GET /api/projects');
    await renderWithVoice(<FeatureBoardScreen projectKey="SHIP" featureKey={KEY} />);

    expect(screen.getByTestId(screenIds.featureBoard)).toBeOnTheScreen();
    expect(screen.queryByTestId(featureBoardStoryId('SHIP-141'))).toBeNull();
    expect(api.calls.filter((c) => c.path.startsWith('/api/projects//'))).toEqual([]);
    expect(api.calls.filter((c) => c.path !== '/api/projects')).toEqual([]);

    await act(async () => api.release('GET /api/projects'));

    expect(await screen.findByTestId(featureBoardStoryId('SHIP-141'))).toBeOnTheScreen();
    expect(api.calls.filter((c) => c.path.includes('//'))).toEqual([]);
  });
});
