import type { FeaturesOverviewDto } from '@shiplean/core/api/featuresOverview';
import type { InboxItemDto } from '@shiplean/core/api/inbox';
import { act, fireEvent, screen, within } from '@testing-library/react-native';
import { links } from '../../links';
import { FeaturesScreen } from '../../screens/FeaturesScreen';
import { screenIds } from '../../screens/testIds';
import { InboxActionsContext } from '../../inbox/InboxActions';
import { InboxSheetsContext } from '../../inbox/InboxSheets';
import { aProject } from '../../test-support/builders';
import {
  aFeatureGroup,
  aFeatureRow,
  anOverview,
  designOverview,
  featuresApi,
} from '../../test-support/features';
import { apiError, type ApiTable } from '../../test-support/fakeApi';
import { anInbox, anInboxItem, fakeInboxActions, fakeInboxSheets } from '../../test-support/inbox';
import { renderWithVoice } from '../../test-support/voiceUi';
import { themes, type Scheme } from '../../theme';
import { countId, primitiveIds, uiIds } from '../../ui/testIds';
import {
  featureDetailsId,
  featureGroupCountId,
  featureGroupId,
  featureMovedId,
  featureNeedsHeaderId,
  featureEpicOptionId,
  featureRollupId,
  featureRowId,
  featureStaleId,
  featureStatusId,
  featureWhoId,
  featuresIds,
} from '../testIds';
import { HIGHLIGHT_MS } from './useFeatureMoves';

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
const OVERVIEW = `GET ${project}/features/overview`;

interface Opts {
  overview?: FeaturesOverviewDto;
  items?: InboxItemDto[];
  table?: ApiTable;
  scheme?: Scheme;
}

async function renderTab({ overview, items = [], table = {}, scheme }: Opts = {}) {
  const api = featuresApi(
    {
      [`GET ${project}/inbox`]: anInbox({ projectId: PROJECT.id, items }),
      [`GET ${project}/members`]: [],
      ...table,
    },
    { projectId: PROJECT.id, overview },
  );
  const actions = fakeInboxActions();
  const sheets = fakeInboxSheets();
  const result = await renderWithVoice(
    <InboxActionsContext.Provider value={actions}>
      <InboxSheetsContext.Provider value={sheets}>
        <FeaturesScreen projectKey="SHIP" />
      </InboxSheetsContext.Provider>
    </InboxActionsContext.Provider>,
    { scheme },
  );
  return { api, ...result };
}

const rowKeys = () =>
  screen
    .getAllByTestId(/^feature-row-/)
    .map((row) => row.props.testID.slice('feature-row-'.length));

beforeEach(() => {
  mockPush.mockClear();
  mockVoice.mockClear();
  mockSpec.mockClear();
});

describe.each(['light', 'dark'] as const)('Features tab (%s)', (scheme) => {
  it('renders the board state: Active, Resolved, New, then Closed folded to one line', async () => {
    await renderTab({ scheme });

    expect(await screen.findByTestId(featureRowId('SHIP-906'))).toBeOnTheScreen();
    expect(screen.getByTestId(screenIds.features)).toBeOnTheScreen();
    const groups = screen.getAllByTestId(/^feature-group-[a-zA-Z]+$/);
    expect(groups.map((g) => g.props.testID)).toEqual([
      featureGroupId('inProgress'),
      featureGroupId('resolved'),
      featureGroupId('proposed'),
      featureGroupId('completed'),
    ]);
    expect(rowKeys()).toEqual(['SHIP-906', 'SHIP-743', 'SHIP-707', 'SHIP-893', 'SHIP-892']);
    expect(screen.getByTestId(featureGroupCountId('inProgress'))).toHaveTextContent('3');
    expect(screen.getByTestId(featureGroupCountId('completed'))).toHaveTextContent('Closed · 2');
    expect(screen.queryByTestId(featureRowId('SHIP-742'))).toBeNull();
    expect(screen.getByTestId(featureStatusId('SHIP-892'))).toHaveTextContent('No status yet.');
    expect(screen.getByTestId(featureRollupId('SHIP-906'))).toHaveTextContent('2/9 stories');
    expect(screen.getByTestId(featuresIds.sinceBanner)).toHaveTextContent(
      '3 changes since Mon 09:12View',
    );
    expect(screen.getByTestId(countId(featuresIds.chipNeeds))).toHaveTextContent('3');
    expect(screen.getByTestId(countId(featuresIds.chipAll))).toHaveTextContent('7');
  });

  it('marks a stale status line in the warn colour and names the agent', async () => {
    const overview = designOverview();
    const first = overview.groups[0]!.rows[0]!;
    first.statusVerified = { ...first.statusVerified!, by: null, agent: 'Claude Code' };
    await renderTab({ scheme, overview });

    const stale = await screen.findByTestId(featureWhoId('SHIP-743'));
    expect(stale).toHaveTextContent('Sam · 4 days old');
    expect(stale).toHaveStyle({ color: themes[scheme].colors.warn });
    expect(screen.getByTestId(featureStaleId('SHIP-743'))).toHaveTextContent('Stale');
    expect(screen.getByTestId(featureWhoId('SHIP-906'))).toHaveTextContent(
      'Claude Code (agent) · 3h ago',
    );
    expect(screen.getByTestId(featureWhoId('SHIP-906'))).not.toHaveStyle({
      color: themes[scheme].colors.warn,
    });
    expect(screen.queryByTestId(featureStaleId('SHIP-906'))).toBeNull();
  });

  it('renders the needs-you state: Inbox rows grouped under their Feature, Stories under it too', async () => {
    const items = [
      anInboxItem({ key: 'SHIP-743', title: 'Nightly backups' }),
      anInboxItem({ key: 'SHIP-906', title: 'Voice commands with on-device models' }),
      anInboxItem({ key: 'SHIP-910', type: 'story', underKey: 'SHIP-906' }),
      anInboxItem({ key: 'SHIP-99', type: 'story', underKey: null }),
    ];
    await renderTab({ scheme, items });
    await fireEvent.press(await screen.findByTestId(featuresIds.chipNeeds));

    expect(await screen.findByTestId(featureNeedsHeaderId('SHIP-906'))).toBeOnTheScreen();
    expect(screen.getAllByTestId(/^feature-needs-header-/).map((h) => h.props.testID)).toEqual([
      featureNeedsHeaderId('SHIP-906'),
      featureNeedsHeaderId('SHIP-743'),
    ]);
    expect(screen.getAllByTestId(/^inbox-row-/).map((r) => r.props.testID)).toEqual([
      'inbox-row-SHIP-906',
      'inbox-row-SHIP-910',
      'inbox-row-SHIP-743',
    ]);
    expect(screen.queryByTestId(featureRowId('SHIP-906'))).toBeNull();
    expect(screen.getByTestId(featuresIds.chipNeeds)).toBeSelected();
  });

  it('narrows to At risk', async () => {
    await renderTab({ scheme });
    await fireEvent.press(await screen.findByTestId(featuresIds.chipRisk));

    expect(rowKeys()).toEqual(['SHIP-906']);
    expect(screen.getByTestId(featureGroupCountId('inProgress'))).toHaveTextContent('1');
    expect(screen.queryByTestId(featureGroupId('resolved'))).toBeNull();
    expect(screen.queryByTestId(featureGroupId('completed'))).toBeNull();
  });

  it('narrows to Blocked', async () => {
    await renderTab({ scheme });
    await fireEvent.press(await screen.findByTestId(featuresIds.chipBlocked));

    expect(rowKeys()).toEqual(['SHIP-743']);
  });

  it('narrows to Needs you', async () => {
    const items = [anInboxItem({ key: 'SHIP-893' })];
    await renderTab({ scheme, items });
    await fireEvent.press(await screen.findByTestId(featuresIds.chipNeeds));

    expect(await screen.findByTestId(featureNeedsHeaderId('SHIP-893'))).toBeOnTheScreen();
    expect(screen.getAllByTestId(/^feature-needs-header-/)).toHaveLength(1);
    await fireEvent.press(screen.getByTestId(featuresIds.chipAll));
    expect(rowKeys()).toHaveLength(5);
  });

  it('opens the digest from the since banner', async () => {
    await renderTab({ scheme });
    await fireEvent.press(await screen.findByTestId(`${featuresIds.sinceBanner}-action`));

    expect(mockPush).toHaveBeenCalledWith(links.digest('SHIP'));
  });

  it('shows No changes when nothing changed', async () => {
    const overview = { ...designOverview(), changedSince: 0, sinceLabel: 'since yesterday' };
    await renderTab({ scheme, overview });

    expect(await screen.findByTestId(featuresIds.sinceBanner)).toHaveTextContent(
      'No changes since yesterday',
    );
    expect(screen.queryByTestId(`${featuresIds.sinceBanner}-action`)).toBeNull();
  });

  it('renders the move-applied state: the moved row highlighted with its old rank', async () => {
    jest.useFakeTimers({ doNotFake: ['nextTick', 'queueMicrotask', 'setImmediate'] });
    try {
      const before = designOverview();
      const after = structuredClone(before);
      const [a, b, c] = after.groups[0]!.rows;
      after.groups[0]!.rows = [c!, a!, b!].map((r, i) => ({ ...r, rank: i + 1 }));
      let reads = 0;
      const { queryClient } = await renderTab({
        scheme,
        overview: before,
        table: { [OVERVIEW]: () => (reads++ === 0 ? before : after) },
      });
      await screen.findByTestId(featureRowId('SHIP-707'));
      expect(screen.queryByTestId(featureMovedId('SHIP-707'))).toBeNull();

      await act(async () => {
        await queryClient.invalidateQueries();
      });

      expect(await screen.findByTestId(featureMovedId('SHIP-707'))).toHaveTextContent('↑ from 3');
      expect(screen.queryByTestId(featureMovedId('SHIP-906'))).toBeNull();
      expect(screen.queryByTestId(featureMovedId('SHIP-743'))).toBeNull();

      await act(async () => {
        jest.advanceTimersByTime(HIGHLIGHT_MS);
      });
      expect(screen.queryByTestId(featureMovedId('SHIP-707'))).toBeNull();
    } finally {
      jest.useRealTimers();
    }
  });

  it('shows no health pill on an on-track Feature', async () => {
    await renderTab({ scheme });

    const onTrack = await screen.findByTestId(featureRowId('SHIP-707'));
    expect(within(onTrack).queryByTestId(primitiveIds.healthPill)).toBeNull();
    expect(
      within(screen.getByTestId(featureRowId('SHIP-906'))).getByTestId(primitiveIds.healthPill),
    ).toHaveTextContent('At risk');
  });
});

describe('Features tab', () => {
  it("narrows to an Epic from the rows' epic", async () => {
    const { api } = await renderTab();
    await fireEvent.press(await screen.findByTestId(featuresIds.chipEpic));
    await fireEvent.press(await screen.findByTestId(featureEpicOptionId('SHIP-700')));

    expect(rowKeys()).toEqual(['SHIP-906', 'SHIP-707']);
    expect(screen.getByTestId(featuresIds.chipEpic)).toHaveTextContent('SHIP-700');
    // No read of the backlog: the choices and the rows are the overview's.
    expect(api.calls.filter((c) => c.path.includes('/backlog'))).toEqual([]);
    expect(api.callsTo(OVERVIEW)).toHaveLength(1);
  });

  it('applies a screen view from voice by turning on the Blocked chip', async () => {
    await renderTab();
    await screen.findByTestId(featureRowId('SHIP-906'));
    const onView = mockVoice.mock.calls.at(-1)![1].onView as (v: unknown) => void;
    // No chip is on yet, so voice rank moves are allowed.
    expect(mockSpec.mock.calls.at(-1)![0].filtering).toBe(false);

    await act(async () => {
      onView({ kind: 'screen', command: 'filter', args: { chip: 'blocked' } });
    });

    expect(screen.getByTestId(featuresIds.chipBlocked)).toBeSelected();
    expect(rowKeys()).toEqual(['SHIP-743']);
    // The spec is told a chip is on, so voice rank moves are refused.
    expect(mockSpec.mock.calls.at(-1)![0].filtering).toBe(true);
  });

  it('expands one row at a time', async () => {
    await renderTab();
    await fireEvent.press(
      within(await screen.findByTestId(featureRowId('SHIP-906'))).getByRole('button'),
    );
    expect(screen.getByTestId(featureDetailsId('SHIP-906'))).toBeOnTheScreen();

    await fireEvent.press(within(screen.getByTestId(featureRowId('SHIP-743'))).getByRole('button'));
    expect(screen.queryByTestId(featureDetailsId('SHIP-906'))).toBeNull();
    expect(screen.getByTestId(featureDetailsId('SHIP-743'))).toBeOnTheScreen();

    await fireEvent.press(
      within(screen.getByTestId(featureRowId('SHIP-743'))).getAllByRole('button')[0]!,
    );
    expect(screen.queryByTestId(featureDetailsId('SHIP-743'))).toBeNull();
  });

  it('shows the empty state', async () => {
    await renderTab({ overview: anOverview([]) });

    expect(await screen.findByText('No Features yet.')).toBeOnTheScreen();
  });

  it('shows the filtered-empty state', async () => {
    await renderTab({
      overview: anOverview([aFeatureGroup('inProgress', [aFeatureRow({ key: 'SHIP-1' })])]),
    });
    await fireEvent.press(await screen.findByTestId(featuresIds.chipBlocked));

    expect(screen.getByText('Nothing matches this filter')).toBeOnTheScreen();
  });

  it('sends no read with an empty project id while the projects read is pending', async () => {
    const { api } = await renderTab({
      table: { 'GET /api/projects': () => new Promise(() => {}) },
    });

    expect(await screen.findByTestId(screenIds.features)).toBeOnTheScreen();
    expect(api.calls.filter((c) => c.path.includes('/projects//'))).toEqual([]);
    expect(api.calls.filter((c) => c.path.includes('/features/'))).toEqual([]);
  });

  it('shows the error state when the project is not in the projects list', async () => {
    await renderTab({ table: { 'GET /api/projects': [] } });

    expect(await screen.findByText("Couldn't load the Features.")).toBeOnTheScreen();
  });

  it('shows the error state', async () => {
    await renderTab({ table: { [OVERVIEW]: () => apiError(500, 'server', 'boom') } });

    expect(await screen.findByText("Couldn't load the Features.")).toBeOnTheScreen();
    expect(screen.getByTestId(uiIds.errorStateAction)).toBeOnTheScreen();
  });
});
