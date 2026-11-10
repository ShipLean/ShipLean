import type { ItemAsksDto } from '@shiplean/core/api/asks';
import type { CommentDto } from '@shiplean/core/api/comments';
import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { FieldChange } from '@shiplean/core/api/persisted';
import type { HistoryDto, RevisionDto } from '@shiplean/core/api/workItems';
import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { InboxActionsContext } from '../../inbox/InboxActions';
import { InboxSheetsContext } from '../../inbox/InboxSheets';
import { links } from '../../links';
import { aProject, aWorkItem } from '../../test-support/builders';
import {
  aFeatureGroup,
  aFeatureRow,
  anOverview,
  FEATURE_STATES,
  featuresApi,
} from '../../test-support/features';
import { apiError, type ApiTable } from '../../test-support/fakeApi';
import { fakeInboxActions, fakeInboxSheets } from '../../test-support/inbox';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { aComment, aDecision, anItemAsks } from '../../test-support/workItem';
import type { Scheme } from '../../theme';
import { FeaturesActionsProvider } from '../FeaturesActions';
import {
  featureApproveId,
  featureChangeStateId,
  featureDecisionId,
  featureDiscussId,
  featureHealthButtonId,
  featureHealthOptionId,
  featureHealthSheetId,
  featureHistoryErrorId,
  featureHistoryLineId,
  featureNewStatusId,
  featureNoteId,
  featureOpenBoardId,
  featureStateApplyId,
  featureStateOptionId,
  featureStateWarningId,
} from '../testIds';
import { FeatureDetails } from './FeatureDetails';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, dismissTo: jest.fn() }),
}));

const PROJECT = aProject();
const KEY = 'SHIP-906';
const ID = 'feature-906-id';
const ITEM = `/api/work-items/${ID}`;
const feature = (overrides: Partial<FeatureRowDto> = {}) =>
  aFeatureRow({ id: ID, key: KEY, ...overrides });

const change = (field: string, to: string): FieldChange => ({
  field,
  from: null,
  to,
  fromLabel: null,
  toLabel: null,
});
const revision = (
  rev: number,
  atLabel: string,
  by: string | null,
  ...changes: FieldChange[]
): RevisionDto => ({
  rev,
  changedBy: null,
  changedAt: '2030-01-01T09:00:00+00:00',
  changedAtLabel: atLabel,
  origin: 'user',
  changes,
  agentName: by,
  webhook: null,
});
const HISTORY: HistoryDto = {
  asks: [],
  revisions: [
    revision(9, '3h ago', 'Claude Code', change('statusLine', 'Fourth, newest.')),
    revision(8, 'yesterday', null, change('title', 'Renamed')),
    revision(7, 'Thu', 'Sam', change('statusLine', 'Third.')),
    revision(6, 'Wed', 'Sam', change('statusLine', 'Second.')),
    revision(5, 'Tue', 'Sam', change('statusLine', 'First, too old.')),
  ],
};

interface Opts {
  row?: FeatureRowDto;
  comments?: CommentDto[];
  asks?: (id: string) => ItemAsksDto;
  history?: HistoryDto;
  table?: ApiTable;
  scheme?: Scheme;
}

async function renderDetails({
  row,
  comments,
  asks,
  history = HISTORY,
  table = {},
  scheme,
}: Opts = {}) {
  const shown = row ?? feature();
  const api = featuresApi(
    {
      [`GET ${ITEM}/comments`]: comments ?? [],
      [`GET ${ITEM}/asks`]: (asks ?? ((id) => anItemAsks(id)))(ID),
      [`GET ${ITEM}/history`]: history,
      ...table,
    },
    {
      projectId: PROJECT.id,
      overview: anOverview([aFeatureGroup('inProgress', [shown])], { projectId: PROJECT.id }),
    },
  );
  const actions = fakeInboxActions();
  const sheets = fakeInboxSheets();
  const result = await renderWithRunner(
    <InboxActionsContext.Provider value={actions}>
      <InboxSheetsContext.Provider value={sheets}>
        <FeaturesActionsProvider>
          <FeatureDetails row={shown} projectId={PROJECT.id} projectKey="SHIP" />
        </FeaturesActionsProvider>
      </InboxSheetsContext.Provider>
    </InboxActionsContext.Provider>,
    { scheme },
  );
  return { api, actions, sheets, ...result };
}

const note = (id: string, body: string, at: string, overrides: Partial<CommentDto> = {}) =>
  aComment('x', { id, body, createdAt: at, createdLabel: at.slice(5, 10), ...overrides });

beforeEach(() => mockPush.mockClear());

describe.each(['light', 'dark'] as const)('Expanded Feature (%s)', (scheme) => {
  it('shows the latest note, the open decision and the last three status lines', async () => {
    await renderDetails({
      scheme,
      row: feature({ statusLine: 'Fourth, newest.' }),
      comments: [
        note('c1', 'An older note.', '2030-01-01T09:00:00+00:00'),
        note(
          'c2',
          'The S22 runs hot after a minute.\nThen it slows down.\nThe fan is off.\nFourth line.',
          '2030-01-03T09:00:00+00:00',
          {
            agentName: 'Claude Code',
          },
        ),
        note('c3', 'Middle note.', '2030-01-02T09:00:00+00:00'),
      ],
      asks: (id) =>
        anItemAsks(id, {
          decisions: [
            aDecision({ number: 3, label: 'D3', question: 'Third question?' }),
            aDecision({ number: 2, label: 'D2', question: 'Second question?' }),
            aDecision({ number: 1, label: 'D1', question: 'Settled?', state: 'confirmed' }),
          ],
        }),
    });

    expect(await screen.findByTestId(featureNoteId(KEY))).toBeOnTheScreen();
    const latest = screen.getByTestId(featureNoteId(KEY));
    expect(latest).toHaveTextContent(/The S22 runs hot after a minute/);
    expect(latest).not.toHaveTextContent(/An older note|Middle note/);
    expect(latest).toHaveTextContent(/Claude Code/);
    // The body is cut to three lines.
    expect(screen.getByText(/The S22 runs hot/).props.numberOfLines).toBe(3);

    // The lowest-numbered open decision, not the confirmed one or the later one.
    const decision = await screen.findByTestId(featureDecisionId(KEY));
    expect(decision).toHaveTextContent(/D2: Second question\?/);
    expect(decision).not.toHaveTextContent(/D1|D3/);

    // Three status lines, newest first, from the history's statusLine changes only.
    expect(await screen.findByTestId(featureHistoryLineId(KEY, 0))).toHaveTextContent(
      'Fourth, newest.Claude Code · 3h ago',
    );
    expect(screen.getByTestId(featureHistoryLineId(KEY, 1))).toHaveTextContent('Third.Sam · Thu');
    expect(screen.getByTestId(featureHistoryLineId(KEY, 2))).toHaveTextContent('Second.Sam · Wed');
    expect(screen.queryByTestId(featureHistoryLineId(KEY, 3))).toBeNull();
    for (const button of [
      featureChangeStateId,
      featureHealthButtonId,
      featureNewStatusId,
      featureOpenBoardId,
    ]) {
      expect(screen.getByTestId(button(KEY))).toBeOnTheScreen();
    }
  });

  it('renders the blocked-answer state: Approve dispatches approve and Discuss opens the discuss sheet', async () => {
    const shown = feature();
    const { actions, sheets } = await renderDetails({
      scheme,
      row: shown,
      asks: (id) =>
        anItemAsks(id, {
          decisions: [aDecision({ number: 10, label: 'D10', question: 'Confirm the colours?' })],
        }),
    });

    await fireEvent.press(await screen.findByTestId(featureApproveId(KEY)));
    expect(actions.dispatched).toEqual([
      {
        area: 'inbox',
        projectId: PROJECT.id,
        itemId: ID,
        key: KEY,
        kind: 'approve',
        numbers: [10],
      },
    ]);

    // Discuss waits for the item (the sheet needs it), then opens the Inbox's sheet on the decision.
    await waitFor(() => expect(screen.getByTestId(featureDiscussId(KEY))).toBeEnabled());
    await fireEvent.press(screen.getByTestId(featureDiscussId(KEY)));
    expect(sheets.calls).toHaveLength(1);
    const [call] = sheets.calls;
    if (call.kind !== 'answer') throw new Error('Expected the answer sheet.');
    expect(call.request.mode).toBe('discuss');
    expect(call.request.projectId).toBe(PROJECT.id);
    expect(call.request.item.key).toBe(KEY);
    expect(call.request.ask.label).toBe('D10');
    expect(call.request.ask.decision?.number).toBe(10);
  });

  it('renders the status-update state: New status opens the status sheet with the current line', async () => {
    const shown = feature({
      revision: 4,
      statusLine: 'Parakeet runs at 1.4x real time on the S22.',
    });
    const { sheets } = await renderDetails({
      scheme,
      row: shown,
      history: { revisions: [], asks: [] },
      table: {
        [`GET /api/projects/${PROJECT.id}/work-items/by-key/${KEY}`]: aWorkItem({
          id: ID,
          key: KEY,
          revision: 4,
          statusLine: shown.statusLine ?? '',
        }),
      },
    });

    // The sheet needs the item, so the button waits for it.
    await waitFor(() => expect(screen.getByTestId(featureNewStatusId(KEY))).toBeEnabled());
    await fireEvent.press(screen.getByTestId(featureNewStatusId(KEY)));
    const [call] = sheets.calls;
    if (call?.kind !== 'answer') throw new Error('Expected the answer sheet.');
    expect(call.request.mode).toBe('status');
    expect(call.request.ask.kind).toBe('status');
    expect(call.request.ask.status?.statusLine).toBe(shown.statusLine);
    expect(call.request.item.key).toBe(KEY);
    expect(call.request.item.revision).toBe(4);
  });

  it('warns that stories are still open when closing and still dispatches', async () => {
    const shown = feature({
      revision: 6,
      rollups: [
        { type: 'story', open: 3, closed: 1, inCurrentSprint: 3, inOtherSprint: 0 },
        { type: 'bug', open: 2, closed: 0, inCurrentSprint: 0, inOtherSprint: 2 },
      ],
    });
    const { runner } = await renderDetails({ scheme, row: shown });
    await fireEvent.press(screen.getByTestId(featureChangeStateId(KEY)));

    // Every state of a Feature, by order; picking a state that does not close says nothing.
    const closed = FEATURE_STATES.find((s) => s.category === 'completed')!;
    const resolved = FEATURE_STATES.find((s) => s.category === 'resolved')!;
    await fireEvent.press(await screen.findByTestId(featureStateOptionId(resolved.name)));
    expect(screen.queryByTestId(featureStateWarningId(KEY))).toBeNull();

    await fireEvent.press(screen.getByTestId(featureStateOptionId(closed.name)));
    expect(screen.getByTestId(featureStateWarningId(KEY))).toHaveTextContent(
      '3 stories and 2 bugs still open',
    );
    // Choosing alone changes nothing.
    expect(runner.dispatched).toEqual([]);

    await fireEvent.press(screen.getByTestId(featureStateApplyId(KEY)));
    expect(runner.dispatched).toHaveLength(1);
    const [{ batch, opts }] = runner.dispatched;
    expect(batch).toEqual([
      {
        area: 'features',
        kind: 'setState',
        itemId: ID,
        key: KEY,
        revision: 6,
        stateId: closed.id,
        stateName: closed.name,
        fromStateId: shown.state.id,
        fromStateName: shown.state.name,
        closing: true,
        openStories: 5,
      },
    ]);
    expect([opts.origin, opts.allOrNothing]).toEqual(['tap', true]);
    expect(screen.queryByTestId(featureStateWarningId(KEY))).toBeNull();
  });

  it('sets health and clears it with Not set', async () => {
    const { runner } = await renderDetails({
      scheme,
      row: feature({ revision: 3, health: 'atRisk' }),
    });

    await fireEvent.press(screen.getByTestId(featureHealthButtonId(KEY)));
    expect(screen.getByTestId(featureHealthSheetId(KEY))).toBeOnTheScreen();
    // The current one is ticked.
    expect(screen.getByTestId(featureHealthOptionId('atRisk'))).toBeSelected();
    expect(screen.getByTestId(featureHealthOptionId('onTrack'))).not.toBeSelected();

    await fireEvent.press(screen.getByTestId(featureHealthOptionId('blocked')));
    expect(screen.queryByTestId(featureHealthSheetId(KEY))).toBeNull();
    await fireEvent.press(screen.getByTestId(featureHealthButtonId(KEY)));
    await fireEvent.press(screen.getByTestId(featureHealthOptionId('notSet')));

    expect(runner.dispatched.map((d) => d.batch[0])).toEqual([
      {
        area: 'features',
        kind: 'setHealth',
        itemId: ID,
        key: KEY,
        revision: 3,
        health: 'blocked',
        from: 'atRisk',
      },
      {
        area: 'features',
        kind: 'setHealth',
        itemId: ID,
        key: KEY,
        revision: 3,
        health: null,
        from: 'atRisk',
      },
    ]);
  });

  it('opens the Feature board', async () => {
    await renderDetails({ scheme });
    await fireEvent.press(screen.getByTestId(featureOpenBoardId(KEY)));
    expect(mockPush).toHaveBeenCalledWith(links.featureBoard('SHIP', KEY));
  });

  it('shows the history error and still shows the buttons', async () => {
    await renderDetails({
      scheme,
      table: { [`GET ${ITEM}/history`]: apiError(500, 'server', 'Boom.') },
    });

    expect(await screen.findByTestId(featureHistoryErrorId(KEY))).toHaveTextContent(
      "Couldn't load the status history.",
    );
    expect(screen.queryByTestId(featureHistoryLineId(KEY, 0))).toBeNull();
    for (const button of [
      featureChangeStateId,
      featureHealthButtonId,
      featureNewStatusId,
      featureOpenBoardId,
    ]) {
      expect(screen.getByTestId(button(KEY))).toBeOnTheScreen();
    }
  });
});
