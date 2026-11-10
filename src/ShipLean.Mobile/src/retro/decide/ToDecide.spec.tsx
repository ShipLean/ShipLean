import type { RetroCardDto, SprintRetroDto } from '@shiplean/core/api/retros';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { UNDO } from '../../actions/undo';
import { aPending, fakeRunner } from '../../test-support/actions';
import { aProject } from '../../test-support/builders';
import { apiError, restoreFetch } from '../../test-support/fakeApi';
import { aSprintRetro, designInsights, retroApi } from '../../test-support/retro';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import type { Scheme } from '../../theme/themes';
import { uiIds } from '../../ui/testIds';
import type { RetroAction } from '../actions';
import { RetroActionsProvider } from '../RetroActions';
import {
  retroAddActionId,
  retroDecideCardId,
  retroDecideCauseId,
  retroDecideLogId,
  retroDecideLogToggleId,
  retroIds,
  retroNotNeededId,
  retroPointId,
  retroProposalId,
  retroProposalKeepId,
  retroProposalMergeId,
} from '../testIds';
import { ToDecideView } from './ToDecideView';

// DecideSheet is RT3b's; this records the point it is opened with.
const mockSheet = jest.fn();
jest.mock('./DecideSheet', () => {
  const { View } = jest.requireActual('react-native');
  return {
    DecideSheet: (props: { open: boolean }) => {
      mockSheet(props);
      return props.open ? <View testID="sheet-retro-decide" /> : null;
    },
  };
});

afterEach(restoreFetch);
beforeEach(() => mockSheet.mockClear());

const project = aProject();
const INSIGHTS = `GET /api/projects/${project.id}/retros/insights`;

const [FEATURE, SAVED_CARDS, BUG, BLOCKED, SPRINT] = (() => {
  const cards = aSprintRetro().cards;
  return [cards[0], cards[1], cards[2], cards[3], cards[4]];
})();

// The fixture's Feature card has no undecided point; give it one and send it last, so "Feature first" is the view's doing.
function retroWithFeaturePoint(): SprintRetroDto {
  const added = {
    ...SAVED_CARDS.points[3],
    id: 'feature-point',
    text: 'Lanes C and D both edited the same migration.',
    rootCause: 'No per-lane migration range in the plan.',
  };
  const feature: RetroCardDto = { ...FEATURE, points: [...FEATURE.points, added] };
  return aSprintRetro({ cards: [SAVED_CARDS, BUG, BLOCKED, SPRINT, feature] });
}

const merge = (kind: 'mergeThemes' | 'keepApart', proposalId: string): RetroAction => ({
  area: 'retro',
  kind,
  itemId: null,
  key: null,
  proposalId,
  fromName: 'Contracts first worked',
  intoName: 'Screenshot baselines need CI',
});

async function renderView(
  scheme: Scheme,
  table: Parameters<typeof retroApi>[0] = {},
  retro: SprintRetroDto = retroWithFeaturePoint(),
  proposals = true,
) {
  const insights = designInsights();
  if (!proposals) insights.mergeProposals = [];
  const fake = retroApi(table, { projectId: project.id, retro, insights });
  const runner = fakeRunner();
  const rendered = await renderWithRunner(
    <RetroActionsProvider>
      <ToDecideView projectId={project.id} sprintId={retro.sprint.id} />
    </RetroActionsProvider>,
    { runner, scheme },
  );
  return { ...rendered, runner, fake, retro, proposal: insights.mergeProposals[0] };
}

const cardIds = () =>
  screen
    .getAllByTestId(/^retro-decide-card-[0-9a-f-]+$|^retro-decide-card-feature-/)
    .map((c) => c.props.testID as string);

describe.each<Scheme>(['light', 'dark'])('Retro to decide (%s)', (scheme) => {
  it('renders the decide state: undecided points by item and the merge proposal card', async () => {
    const { proposal } = await renderView(scheme);
    await screen.findByTestId(retroProposalId(proposal.id));

    // Feature card first, though the server sent it last; then the others in the server's order.
    expect(cardIds()).toEqual(
      [FEATURE, SAVED_CARDS, BUG, BLOCKED, SPRINT].map((c) => retroDecideCardId(c.id)),
    );
    expect(screen.getByText('SHIP-101')).toBeTruthy();
    expect(screen.getByText('Checkout v2')).toBeTruthy();
    expect(screen.getAllByText(/^Retro by Claude Code$/).length).toBeGreaterThan(0);
    expect(screen.getByText('This sprint')).toBeTruthy();

    // Undecided points only, each with its two buttons; the root cause where there is one.
    expect(screen.getByText('Lanes C and D both edited the same migration.')).toBeTruthy();
    expect(screen.getByTestId(retroDecideCauseId('feature-point'))).toHaveTextContent(
      'Root cause: No per-lane migration range in the plan.',
    );
    expect(screen.queryByText(/^Lanes A and C both added a mig/)).toBeNull();
    expect(screen.queryByText(/^Plan: give each lane/)).toBeNull();
    expect(screen.queryByText(/^The card token contract was on/)).toBeNull();
    expect(screen.getByTestId(retroAddActionId('feature-point'))).toBeTruthy();
    expect(screen.getByTestId(retroNotNeededId('feature-point'))).toBeTruthy();
    // Six undecided points on the five cards: 1 + 2 + 1 + 1 + 1.
    expect(screen.getAllByTestId(/-add-action$/)).toHaveLength(6);
    // The proposal: who, what, why, how many points.
    const card = screen.getByTestId(retroProposalId(proposal.id));
    expect(
      within(card).getByText(
        'Claude Code · decide proposes merging Contracts first worked into Screenshot baselines need CI',
      ),
    ).toBeTruthy();
    expect(within(card).getByText('Both are about slow, flaky CI. · 3 points')).toBeTruthy();
    expect(screen.getByText('Merge')).toBeTruthy();
    expect(screen.getByText('Keep apart')).toBeTruthy();
    // Proposals come before the cards.
    const order = screen
      .getAllByTestId(/^retro-proposal-[0-9a-f-]+$|^retro-decide-card-[0-9a-f-]+$/)
      .map((n) => n.props.testID as string);
    expect(order[0]).toBe(retroProposalId(proposal.id));
    expect(order[1]).toBe(retroDecideCardId(FEATURE.id));
  });

  it('dispatches mergeThemes and keepApart from the card', async () => {
    const { runner, proposal } = await renderView(scheme);
    await fireEvent.press(await screen.findByTestId(retroProposalMergeId(proposal.id)));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([merge('mergeThemes', proposal.id)]);
    expect(runner.dispatched[0].opts).toMatchObject({
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });

    await fireEvent.press(screen.getByTestId(retroProposalKeepId(proposal.id)));
    expect(runner.dispatched).toHaveLength(2);
    expect(runner.dispatched[1].batch).toEqual([merge('keepApart', proposal.id)]);
  });

  it('hides a proposal while its answer is held or sent, and shows it again when the answer failed', async () => {
    const { runner, proposal } = await renderView(scheme);
    await screen.findByTestId(retroProposalId(proposal.id));
    const answer = (state: 'undo' | 'failed') =>
      act(() =>
        runner.setPending([aPending({ state, action: merge('mergeThemes', proposal.id) })]),
      );
    await answer('undo');
    expect(screen.queryByTestId(retroProposalId(proposal.id))).toBeNull();
    await answer('failed');
    expect(screen.getByTestId(retroProposalId(proposal.id))).toBeTruthy();
  });

  it('folds the raw failure log until tapped', async () => {
    await renderView(scheme);
    const toggle = await screen.findByTestId(retroDecideLogToggleId(SAVED_CARDS.id));
    expect(toggle).toHaveTextContent(/Raw failure log · 3 entries/);
    expect(screen.queryByTestId(retroDecideLogId(SAVED_CARDS.id))).toBeNull();
    expect(screen.queryByText(/Executable doesn't exist/)).toBeNull();

    await fireEvent.press(toggle);
    const log = screen.getByTestId(retroDecideLogId(SAVED_CARDS.id));
    expect(within(log).getByText('Blockers')).toBeTruthy();
    expect(within(log).getByText('Assumptions disproved')).toBeTruthy();
    expect(within(log).getByText('Friction')).toBeTruthy();
    expect(within(log).getByText(/Executable doesn't exist/)).toBeTruthy();
    // The Feature card is open and the others stay folded.
    expect(screen.queryByTestId(retroDecideLogId(BLOCKED.id))).toBeNull();

    await fireEvent.press(toggle);
    expect(screen.queryByTestId(retroDecideLogId(SAVED_CARDS.id))).toBeNull();
    // A card whose log is empty has nothing to open.
    expect(screen.queryByTestId(retroDecideLogToggleId(BUG.id))).toBeNull();
  });

  it('opens the decide sheet for + Action item and Not needed', async () => {
    await renderView(scheme);
    expect(screen.queryByTestId(retroIds.decideSheet)).toBeNull();
    const [first, second] = [SAVED_CARDS.points[1], SAVED_CARDS.points[3]];

    await fireEvent.press(await screen.findByTestId(retroAddActionId(first.id)));
    expect(screen.getByTestId(retroIds.decideSheet)).toBeTruthy();
    expect(mockSheet.mock.calls.at(-1)![0]).toMatchObject({
      point: { id: first.id },
      projectId: project.id,
      open: true,
    });

    await act(() => mockSheet.mock.calls.at(-1)![0].onClose());
    expect(screen.queryByTestId(retroIds.decideSheet)).toBeNull();

    await fireEvent.press(screen.getByTestId(retroNotNeededId(second.id)));
    expect(screen.getByTestId(retroIds.decideSheet)).toBeTruthy();
    expect(mockSheet.mock.calls.at(-1)![0]).toMatchObject({ point: { id: second.id } });
    expect(screen.getByTestId(retroPointId(second.id))).toBeTruthy();
  });

  it('shows the insights error and still lists the points', async () => {
    const { fake } = await renderView(scheme, {
      [INSIGHTS]: apiError(500, 'internal', 'Insights are down.'),
    });
    await screen.findByTestId(retroIds.toDecideInsightsError);
    expect(screen.getByTestId(retroDecideCardId(FEATURE.id))).toBeTruthy();
    expect(screen.getByText('Lanes C and D both edited the same migration.')).toBeTruthy();
    expect(screen.queryByTestId(retroIds.toDecideEmpty)).toBeNull();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();

    const reads = fake.callsTo(INSIGHTS).length;
    await fireEvent.press(screen.getByTestId(retroIds.toDecideInsightsRetry));
    await waitFor(() => expect(fake.callsTo(INSIGHTS).length).toBeGreaterThan(reads));
  });

  it('shows the retro error with Retry, and the empty state', async () => {
    const empty = aSprintRetro({ cards: [] });
    const failing = await renderView(scheme, {}, empty, false);
    await screen.findByTestId(retroIds.toDecideEmpty);
    expect(screen.getByText('Nothing to decide.')).toBeTruthy();
    await failing.unmount();

    const retro = retroWithFeaturePoint();
    await renderView(scheme, {
      [`GET /api/sprints/${retro.sprint.id}/retro`]: apiError(500, 'internal', 'Down.'),
    });
    await screen.findByTestId(uiIds.errorState);
    expect(screen.getByTestId(retroIds.toDecide)).toBeTruthy();
    expect(screen.getByTestId(uiIds.errorStateAction)).toBeTruthy();
  });
});
