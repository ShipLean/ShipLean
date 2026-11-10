import type { RetroActionRefDto, RetroPointDto, SprintRetroDto } from '@shiplean/core/api/retros';
import { act, fireEvent, screen } from '@testing-library/react-native';
import { UNDO } from '../../actions/undo';
import { RetroScreen } from '../../screens/RetroScreen';
import { RetroProvider } from '../../shell/providers/retro';
import { aPending, fakeRunner, type FakeRunner } from '../../test-support/actions';
import { aMe, aProject, aSprint } from '../../test-support/builders';
import { apiError } from '../../test-support/fakeApi';
import { aRetroPoint, designRetro, retroApi } from '../../test-support/retro';
import { signedInApi } from '../../test-support/signedInApi';
import { renderWithVoice } from '../../test-support/voiceUi';
import { primitiveIds, optionCountId, optionId, uiIds } from '../../ui/testIds';
import { screenIds } from '../../screens/testIds';
import type { RetroAction } from '../actions';
import {
  retroActionKeyId,
  retroAddActionId,
  retroFaceId,
  retroIds,
  retroNotNeededId,
  retroNotNeededReasonId,
  retroPlusId,
  retroPointId,
  retroTileCountId,
  retroTileId,
  retroTileToDecideId,
} from '../testIds';

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
const ME = aMe();

const everyPoint = (retro: SprintRetroDto): RetroPointDto[] =>
  retro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
const pointText = (retro: SprintRetroDto, text: string): RetroPointDto => {
  const found = everyPoint(retro).find((p) => p.text === text);
  if (found === undefined) throw new Error(`The design retro has no point "${text}".`);
  return found;
};

const retroRoute = (retro: SprintRetroDto) => `GET /api/sprints/${retro.sprint.id}/retro`;

function makeApi(
  retro: SprintRetroDto = designRetro(),
  table: Parameters<typeof retroApi>[0] = {},
) {
  return retroApi(
    { ...signedInApi(), 'GET /api/me': ME, ...table },
    { projectId: PROJECT.id, retro },
  );
}

async function renderRetro(scheme: 'light' | 'dark' = 'light', runner: FakeRunner = fakeRunner()) {
  const result = await renderWithVoice(
    <RetroProvider projectKey="SHIP">
      <RetroScreen projectKey="SHIP" />
    </RetroProvider>,
    { runner, scheme },
  );
  return { ...result, runner };
}

const support = (pointId: string, kind: 'support' | 'unsupport' = 'support'): RetroAction => ({
  area: 'retro',
  kind,
  itemId: null,
  key: null,
  pointId,
});

const lastOnView = () => mockVoice.mock.calls.at(-1)![1].onView as (v: unknown) => void;

// A category's points regrouped, so the theme order is not the server's.
function withBadlyGroups(retro: SprintRetroDto): SprintRetroDto {
  const [slow, baselines, loose] = [
    'The S22 build took 40 minutes.',
    'Baselines had to be regenerated from CI twice.',
    'Two agents edited the same file.',
  ].map((text) => pointText(retro, text));
  const second = aRetroPoint({
    category: 'wentBadly',
    text: 'Baselines went stale on main again.',
    theme: baselines.theme,
    recurring: true,
    decision: 'undecided',
    notNeeded: null,
    action: null,
    supporters: [slow.author.user],
    supportedByMe: false,
  });
  return {
    ...retro,
    categories: retro.categories.map((c) =>
      c.category === 'wentBadly'
        ? {
            ...c,
            groups: [
              { theme: null, recurring: false, points: [loose] },
              { theme: slow.theme, recurring: false, points: [slow] },
              { theme: baselines.theme, recurring: true, points: [baselines, second] },
            ],
          }
        : c,
    ),
  };
}

const actionRef = (key: string): RetroActionRefDto => ({
  workItemId: `wi-${key}`,
  key,
  title: 'Cap test-running agents',
  state: { id: 'active', name: 'Active', category: 'inProgress' },
  sprint: null,
  points: null,
  fixType: 'guide',
  pointCount: 1,
});

const emptyRetro = (retro: SprintRetroDto, sprint: SprintRetroDto['sprint']): SprintRetroDto => ({
  ...retro,
  sprint,
  kpis: { ...retro.kpis, pointsToDecide: 0 },
  categories: retro.categories.map((c) => ({ ...c, groups: [], toDecide: 0 })),
});

describe('Retro tab', () => {
  beforeEach(() => {
    mockPush.mockClear();
    mockVoice.mockClear();
  });

  describe.each(['light', 'dark'] as const)('in %s', (scheme) => {
    it('renders the retro state: tiles in web order, the first category with points to decide open (Not in the plan with designRetro()), themes largest first', async () => {
      const retro = withBadlyGroups(designRetro());
      makeApi(retro);
      await renderRetro(scheme);

      await screen.findByTestId(retroIds.tiles);
      expect(
        screen
          .getAllByTestId(/^retro-tile-[a-zA-Z]+$/)
          .map((t) => t.props.testID.slice('retro-tile-'.length)),
      ).toEqual(['wentWell', 'notInPlan', 'wentBadly', 'improve']);
      expect(screen.getByTestId(retroTileId('notInPlan'))).toBeSelected();
      expect(screen.getByTestId(retroTileId('wentBadly'))).not.toBeSelected();
      // Counts and what is left to decide, per tile.
      expect(screen.getByTestId(retroTileCountId('wentWell'))).toHaveTextContent('2');
      expect(screen.getByTestId(retroTileCountId('wentBadly'))).toHaveTextContent('4');
      expect(screen.queryByTestId(retroTileToDecideId('wentWell'))).toBeNull();
      expect(screen.getByTestId(retroTileToDecideId('notInPlan'))).toHaveTextContent('1 to decide');
      expect(screen.getByText('The signing key expired mid-sprint.')).toBeTruthy();
      expect(screen.queryByText('The S22 build took 40 minutes.')).toBeNull();

      // The top bar: the sprint, its dates from getSprints, and how many retros are logged.
      expect(screen.getByText('Sprint 25 retro')).toBeTruthy();
      expect(screen.getByText(`${aSprint().datesLabel} · 4 retros logged`)).toBeTruthy();
      expect(screen.getByTestId(optionCountId(retroIds.segment, 'toDecide'))).toHaveTextContent(
        String(retro.kpis.pointsToDecide),
      );

      await fireEvent.press(screen.getByTestId(retroTileId('wentBadly')));
      expect(screen.getByTestId(retroTileId('wentBadly'))).toBeSelected();
      const order = screen
        .getAllByTestId(/^retro-theme-(?!.*-recurring$)/)
        .map((h) => h.props.testID.slice('retro-theme-'.length));
      const [slow, baselines] = [
        'The S22 build took 40 minutes.',
        'Baselines had to be regenerated from CI twice.',
      ].map((text) => pointText(retro, text).theme!.id);
      // Two points, one point, then the unthemed group.
      expect(order).toEqual([baselines, slow, 'none']);
      expect(screen.getAllByText('Recurring')).toHaveLength(1);
      expect(screen.getByText('Unthemed')).toBeTruthy();
      // More supporters first inside a theme.
      expect(screen.getAllByTestId(/-text$/).map((t) => t.props.children)).toEqual([
        'Baselines went stale on main again.',
        'Baselines had to be regenerated from CI twice.',
        'The S22 build took 40 minutes.',
        'Two agents edited the same file.',
      ]);
    });

    it('renders the agree state: +1 dispatches support and shows my face', async () => {
      const retro = designRetro();
      makeApi(retro);
      const { runner } = await renderRetro(scheme);
      await screen.findByTestId(retroIds.tiles);
      await fireEvent.press(screen.getByTestId(retroTileId('wentBadly')));
      const point = pointText(retro, 'Two agents edited the same file.');

      expect(screen.queryByTestId(retroFaceId(point.id, ME.id))).toBeNull();
      await fireEvent.press(screen.getByTestId(retroPlusId(point.id)));

      expect(runner.dispatched).toHaveLength(1);
      expect(runner.dispatched[0].batch).toEqual([support(point.id)]);
      expect(runner.dispatched[0].opts.undo).toEqual(UNDO.none);

      // The runner now holds the +1: the card shows me and the button is on.
      await act(async () => runner.setPending([aPending({ action: support(point.id) })]));
      expect(screen.getByTestId(retroFaceId(point.id, ME.id))).toBeTruthy();
      expect(screen.getByTestId(retroPlusId(point.id))).toBeSelected();
      expect(screen.getByTestId(retroPlusId(point.id))).toHaveTextContent('1');
      expect(screen.getByTestId(retroPlusId(point.id))).not.toHaveTextContent('+1');
    });

    it('takes a +1 back with a second tap', async () => {
      const retro = designRetro();
      makeApi(retro);
      const { runner } = await renderRetro(scheme);
      await screen.findByTestId(retroIds.tiles);
      await fireEvent.press(screen.getByTestId(retroTileId('wentBadly')));
      const point = pointText(retro, 'Two agents edited the same file.');

      await fireEvent.press(screen.getByTestId(retroPlusId(point.id)));
      await act(async () => runner.setPending([aPending({ action: support(point.id) })]));
      await fireEvent.press(screen.getByTestId(retroPlusId(point.id)));

      expect(runner.dispatched.map((d) => d.batch[0])).toEqual([
        support(point.id),
        support(point.id, 'unsupport'),
      ]);
      await act(async () =>
        runner.setPending([
          aPending({ id: 'p1', action: support(point.id) }),
          aPending({ id: 'p2', action: support(point.id, 'unsupport') }),
        ]),
      );
      expect(screen.queryByTestId(retroFaceId(point.id, ME.id))).toBeNull();
      expect(screen.getByTestId(retroPlusId(point.id))).not.toBeSelected();
    });

    it('highlights the last touched point', async () => {
      const retro = designRetro();
      makeApi(retro);
      await renderRetro(scheme);
      await screen.findByTestId(retroIds.tiles);
      await fireEvent.press(screen.getByTestId(retroTileId('wentBadly')));
      const [first, other] = [
        'The S22 build took 40 minutes.',
        'Two agents edited the same file.',
      ].map((text) => pointText(retro, text).id);

      expect(screen.getByTestId(retroPointId(first))).not.toBeSelected();
      await fireEvent.press(screen.getByTestId(retroPointId(first)));
      expect(screen.getByTestId(retroPointId(first))).toBeSelected();
      expect(screen.getByTestId(retroPointId(other))).not.toBeSelected();

      // A +1 touches too, and the highlight moves.
      await fireEvent.press(screen.getByTestId(retroPlusId(other)));
      expect(screen.getByTestId(retroPointId(other))).toBeSelected();
      expect(screen.getByTestId(retroPointId(first))).not.toBeSelected();
    });

    it('shows no decision buttons on Went well and the action key once actioned', async () => {
      const base = designRetro();
      const well = pointText(base, 'Reviews came back within the hour.');
      const actioned = aRetroPoint({
        category: 'improve',
        text: 'Cap test-running agents at three.',
        decision: 'actioned',
        action: actionRef('SHIP-958'),
        theme: null,
      });
      const declined = aRetroPoint({
        category: 'improve',
        text: 'Name the files each lane owns.',
        decision: 'notNeeded',
        notNeeded: {
          reason: 'SHIP-958 covers it.',
          by: well.author,
          decidedAtLabel: 'Today',
        },
        theme: null,
      });
      const open = pointText(base, 'Profile the small model first.');
      const retro: SprintRetroDto = {
        ...base,
        categories: base.categories.map((c) => {
          if (c.category === 'improve')
            return {
              ...c,
              groups: [{ theme: null, recurring: false, points: [open, actioned, declined] }],
            };
          return c;
        }),
      };
      makeApi(retro);
      await renderRetro(scheme);
      await screen.findByTestId(retroIds.tiles);

      await fireEvent.press(screen.getByTestId(retroTileId('wentWell')));
      expect(screen.getByTestId(retroPointId(well.id))).toBeTruthy();
      expect(screen.queryByTestId(retroAddActionId(well.id))).toBeNull();
      expect(screen.queryByTestId(retroNotNeededId(well.id))).toBeNull();
      expect(screen.queryByTestId(retroActionKeyId(well.id))).toBeNull();

      await fireEvent.press(screen.getByTestId(retroTileId('improve')));
      // Undecided: both buttons.
      expect(screen.getByTestId(retroAddActionId(open.id))).toHaveTextContent('+ Action item');
      expect(screen.getByTestId(retroNotNeededId(open.id))).toHaveTextContent('Not needed');
      // Actioned: the action's key and state, and no buttons.
      expect(screen.getByTestId(retroActionKeyId(actioned.id))).toHaveTextContent(/^SHIP-958/);
      expect(screen.getByText('Active')).toBeTruthy();
      expect(screen.queryByTestId(retroAddActionId(actioned.id))).toBeNull();
      // Not needed: the reason.
      expect(screen.getByTestId(retroNotNeededReasonId(declined.id))).toHaveTextContent(
        'Not neededSHIP-958 covers it.',
      );
      expect(screen.queryByTestId(retroNotNeededId(declined.id))).toBeNull();

      await fireEvent.press(screen.getByTestId(retroActionKeyId(actioned.id)));
      expect(mockPush).toHaveBeenCalledWith({
        pathname: '/p/[key]/items/[itemKey]',
        params: { key: 'SHIP', itemKey: 'SHIP-958' },
      });
    });

    it('opens the Decide sheet from + Action item and from Not needed', async () => {
      const retro = designRetro();
      makeApi(retro);
      await renderRetro(scheme);
      await screen.findByTestId(retroIds.tiles);
      const point = pointText(retro, 'The signing key expired mid-sprint.');

      expect(screen.queryByTestId(retroIds.decideSheet)).toBeNull();
      await fireEvent.press(screen.getByTestId(retroAddActionId(point.id)));
      expect(screen.getByTestId(retroIds.decideSheet)).toBeTruthy();
    });

    it('reads the retro again when a +1 has been sent', async () => {
      const retro = designRetro();
      const api = makeApi(retro);
      const { runner } = await renderRetro(scheme);
      await screen.findByTestId(retroIds.tiles);
      const reads = () => api.callsTo(retroRoute(retro)).length;
      const before = reads();
      const point = pointText(retro, 'The signing key expired mid-sprint.');

      await act(async () =>
        runner.setPending([aPending({ action: support(point.id), state: 'sending' })]),
      );
      expect(reads()).toBe(before);
      await act(async () =>
        runner.setPending([aPending({ action: support(point.id), state: 'sent' })]),
      );
      await screen.findByTestId(retroIds.tiles);
      expect(reads()).toBe(before + 1);
    });

    it('shows the ended sprint until the next one has a point', async () => {
      const base = designRetro();
      const ended = { ...base.sprint, id: 'sprint-25', name: 'Sprint 25', status: 'past' as const };
      const next = {
        ...base.sprint,
        id: 'sprint-26',
        name: 'Sprint 26',
        status: 'current' as const,
      };
      const sprints = [
        aSprint({ id: next.id, name: next.name, status: 'current' }),
        aSprint({ id: ended.id, name: ended.name, status: 'past', endDate: '2000-01-01' }),
      ];
      const endedRetro = { ...base, sprint: ended };
      const nextRetro = emptyRetro(base, next);
      const table = {
        [`GET /api/projects/${PROJECT.id}/sprints`]: sprints,
        [retroRoute(endedRetro)]: endedRetro,
        [retroRoute(nextRetro)]: nextRetro,
      };
      makeApi(base, table);
      const first = await renderRetro(scheme);

      // Sprint 26 has no point yet: Sprint 25's retro is shown.
      expect(await screen.findByText('Sprint 25 retro')).toBeTruthy();
      expect(screen.queryByText('Sprint 26 retro')).toBeNull();
      await first.unmount();

      // Its first point arrives: the current sprint's retro is shown.
      const withPoint = {
        ...nextRetro,
        categories: nextRetro.categories.map((c) =>
          c.category === 'wentWell'
            ? {
                ...c,
                groups: [
                  {
                    theme: null,
                    recurring: false,
                    points: [aRetroPoint({ category: 'wentWell', decision: null })],
                  },
                ],
              }
            : c,
        ),
      };
      makeApi(base, { ...table, [retroRoute(nextRetro)]: withPoint });
      await renderRetro(scheme);
      expect(await screen.findByText('Sprint 26 retro')).toBeTruthy();
      expect(screen.queryByText('Sprint 25 retro')).toBeNull();
    });

    it('applies a screen view from voice: show To decide picks the segment', async () => {
      const retro = designRetro();
      makeApi(retro);
      await renderRetro(scheme);
      await screen.findByTestId(retroIds.tiles);
      expect(screen.getByTestId(optionId(retroIds.segment, 'notes'))).toBeSelected();

      await act(async () => {
        lastOnView()({ kind: 'screen', command: 'show', args: { view: 'toDecide' } });
      });
      expect(screen.getByTestId(optionId(retroIds.segment, 'toDecide'))).toBeSelected();
      expect(screen.getByTestId(retroIds.toDecide)).toBeTruthy();

      await act(async () => {
        lastOnView()({ kind: 'screen', command: 'show', args: { view: 'features' } });
      });
      expect(screen.getByTestId(retroIds.features)).toBeTruthy();

      // A category goes back to Notes and picks its tile.
      await act(async () => {
        lastOnView()({ kind: 'screen', command: 'show', args: { view: 'wentBadly' } });
      });
      expect(screen.getByTestId(optionId(retroIds.segment, 'notes'))).toBeSelected();
      expect(screen.getByTestId(retroTileId('wentBadly'))).toBeSelected();

      // Anything else is ignored.
      await act(async () => {
        lastOnView()({ kind: 'screen', command: 'show', args: { view: 'nowhere' } });
      });
      expect(screen.getByTestId(retroTileId('wentBadly'))).toBeSelected();
    });

    it('shows the empty state', async () => {
      const base = designRetro();
      const retro = emptyRetro(base, base.sprint);
      makeApi(retro);
      await renderRetro(scheme);

      expect(await screen.findByText('No points in Went well yet.')).toBeTruthy();
      expect(screen.getByTestId(retroTileCountId('wentWell'))).toHaveTextContent('0');
      await fireEvent.press(screen.getByTestId(retroTileId('improve')));
      expect(screen.getByText('No points in Can be improved yet.')).toBeTruthy();
    });

    it('shows the error state and retries', async () => {
      const retro = designRetro();
      const api = makeApi(retro);
      api.failNext(retroRoute(retro), apiError(500, 'server_error'));
      await renderRetro(scheme);

      expect(await screen.findByText("Couldn't load the retro.")).toBeTruthy();
      expect(screen.getByTestId(screenIds.retro)).toBeTruthy();
      await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));
      expect(await screen.findByTestId(retroIds.tiles)).toBeTruthy();
      expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
    });

    it('shows the no-sprint state', async () => {
      makeApi(designRetro(), { [`GET /api/projects/${PROJECT.id}/sprints`]: [] });
      await renderRetro(scheme);

      expect(await screen.findByText('No sprint yet.')).toBeTruthy();
      expect(screen.getByTestId(screenIds.retro)).toBeTruthy();
      expect(screen.queryByTestId(retroIds.addPoint)).toBeNull();
    });

    it('sends no read with an empty project id on a cold open', async () => {
      const retro = designRetro();
      const api = makeApi(retro);
      api.hang('GET /api/projects');
      await renderRetro(scheme);

      expect(screen.getByTestId(screenIds.retro)).toBeTruthy();
      expect(await screen.findByTestId(primitiveIds.skeleton)).toBeTruthy();
      expect(
        api.calls
          .map((c) => c.path)
          .filter((path) => path.includes('/projects//') || path.endsWith('/sprints//retro')),
      ).toEqual([]);
      expect(screen.queryByTestId(retroIds.tiles)).toBeNull();

      api.release('GET /api/projects');
      expect(await screen.findByTestId(retroIds.tiles)).toBeTruthy();
    });

    it('keeps the screen root while loading', async () => {
      const retro = designRetro();
      const api = makeApi(retro);
      api.hang(retroRoute(retro));
      await renderRetro(scheme);

      expect(screen.getByTestId(screenIds.retro)).toBeTruthy();
      expect(await screen.findByTestId(primitiveIds.skeleton)).toBeTruthy();
      api.release(retroRoute(retro));
      expect(await screen.findByTestId(retroIds.tiles)).toBeTruthy();
    });
  });
});
