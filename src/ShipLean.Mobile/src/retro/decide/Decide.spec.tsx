import type { MemberDto } from '@shiplean/core/api/projects';
import type {
  RetroActionDetailDto,
  RetroActionRefDto,
  RetroPointDto,
} from '@shiplean/core/api/retros';
import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import actionDetail from '../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-action-detail.json';
import { fakeRunner } from '../../test-support/actions';
import { aMember, aProject } from '../../test-support/builders';
import { apiError, restoreFetch } from '../../test-support/fakeApi';
import { designRetro, retroApi } from '../../test-support/retro';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import type { Scheme } from '../../theme/themes';
import { optionId, uiIds } from '../../ui/testIds';
import type { RetroAction } from '../actions';
import { RetroActionsProvider } from '../RetroActions';
import { retroDecideExistingId, retroDecideOwnerId, retroIds } from '../testIds';
import { DecideSheet } from './DecideSheet';

afterEach(restoreFetch);

const project = aProject();
const DIALOG = `GET /api/projects/${project.id}/retros/action-dialog`;
const MEMBERS = `GET /api/projects/${project.id}/members`;

// The design's point: "Two agents edited the same file." has no theme; the S22 build point has one.
const retro = designRetro();
const points = retro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
const point: RetroPointDto = {
  ...points.find((p) => p.text === 'The S22 build took 40 minutes.')!,
  text: 'Give each lane its own migration number range in the plan, so two lanes never collide on one file again.',
};
const priya: MemberDto = aMember({
  user: { ...aMember().user, id: 'user-priya', name: 'Priya Shah', initials: 'PS' },
});
const tom: MemberDto = aMember({
  user: { ...aMember().user, id: 'user-tom', name: 'Tom Reid', initials: 'TR' },
});

async function renderSheet(scheme: Scheme, table: Parameters<typeof retroApi>[0] = {}) {
  const fake = retroApi({ [MEMBERS]: [priya, tom], ...table }, { projectId: project.id, retro });
  const runner = fakeRunner();
  const onClose = jest.fn();
  const rendered = await renderWithRunner(
    <RetroActionsProvider>
      <DecideSheet point={point} projectId={project.id} open onClose={onClose} />
    </RetroActionsProvider>,
    { runner, scheme },
  );
  return { ...rendered, runner, fake, onClose };
}

const mode = (value: string) =>
  fireEvent.press(screen.getByTestId(optionId(retroIds.decideMode, value)));

describe.each<Scheme>(['light', 'dark'])('Retro decide sheet (%s)', (scheme) => {
  it('renders the action-preview state: a new action for Priya from the point text', async () => {
    const { runner, onClose } = await renderSheet(scheme);
    // The title is the point's text word for word, cut at 80 characters as the web seeds it.
    expect(screen.getByTestId(retroIds.decideTitle).props.value).toBe(point.text.slice(0, 80));
    await fireEvent.press(await screen.findByTestId(retroDecideOwnerId('user-priya')));
    expect(screen.getByTestId(retroDecideOwnerId('user-priya'))).toBeSelected();
    expect(screen.getByTestId(retroIds.decideNoOwner)).not.toBeSelected();
    await fireEvent.press(screen.getByTestId(retroIds.decideSubmit));

    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual<RetroAction[]>([
      {
        area: 'retro',
        kind: 'newAction',
        itemId: null,
        key: null,
        projectId: project.id,
        pointIds: [point.id],
        title: point.text.slice(0, 80),
        ownerId: 'user-priya',
        ownerName: 'Priya Shah',
        themeId: point.theme?.id ?? null,
      },
    ]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('makes an action with no owner, and keeps a typed title as typed', async () => {
    const { runner } = await renderSheet(scheme);
    await fireEvent.changeText(screen.getByTestId(retroIds.decideTitle), '  Own migration ranges ');
    await fireEvent.press(screen.getByTestId(retroIds.decideSubmit));
    expect(runner.dispatched[0].batch[0]).toMatchObject({
      kind: 'newAction',
      title: 'Own migration ranges',
      ownerId: null,
      ownerName: null,
    });
  });

  it('lists open action items for Add to existing', async () => {
    const open = [
      { ...aOpen('SHIP-958', 'Per-lane migration ranges'), workItemId: 'wi-958' },
      { ...aOpen('SHIP-940', 'Pin the CI baselines'), workItemId: 'wi-940' },
    ];
    const { runner, fake } = await renderSheet(scheme, {
      [DIALOG]: { selected: [point], offered: [], openActions: open, suggestedTheme: null },
    });
    await mode('existing');
    await screen.findByTestId(retroDecideExistingId('SHIP-958'));
    expect(screen.getByText('SHIP-958 Per-lane migration ranges')).toBeTruthy();
    expect(screen.getByText('SHIP-940 Pin the CI baselines')).toBeTruthy();
    expect(fake.callsTo(DIALOG).map((c) => c.url.slice(c.origin.length))).toEqual([
      `/api/projects/${project.id}/retros/action-dialog?point=${point.id}`,
    ]);

    await fireEvent.press(screen.getByTestId(retroDecideExistingId('SHIP-940')));
    await fireEvent.press(screen.getByTestId(retroIds.decideSubmit));
    expect(runner.dispatched[0].batch).toEqual<RetroAction[]>([
      {
        area: 'retro',
        kind: 'addToAction',
        itemId: 'wi-940',
        key: 'SHIP-940',
        pointIds: [point.id],
      },
    ]);
  });

  it('dispatches notNeeded with an empty reason', async () => {
    const { runner, onClose } = await renderSheet(scheme);
    await mode('notNeeded');
    // An empty reason is allowed (P-R4): the button is live with nothing typed.
    expect(screen.getByTestId(retroIds.decideSubmit)).toBeEnabled();
    await fireEvent.press(screen.getByTestId(retroIds.decideSubmit));
    expect(runner.dispatched[0].batch).toEqual<RetroAction[]>([
      {
        area: 'retro',
        kind: 'notNeeded',
        itemId: null,
        key: null,
        pointId: point.id,
        reason: '',
      },
    ]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('sends a typed reason as typed', async () => {
    const { runner } = await renderSheet(scheme);
    await mode('notNeeded');
    await fireEvent.changeText(screen.getByTestId(retroIds.decideReason), 'Fixed in Sprint 23');
    await fireEvent.press(screen.getByTestId(retroIds.decideSubmit));
    expect(runner.dispatched[0].batch[0]).toMatchObject({ reason: 'Fixed in Sprint 23' });
  });

  it('shows the action dialog read error with Retry', async () => {
    const { fake } = await renderSheet(scheme, {
      [DIALOG]: apiError(500, 'server', 'The action dialog failed.'),
    });
    await mode('existing');
    await screen.findByTestId(uiIds.errorState);
    expect(screen.getByText("Couldn't load the open action items.")).toBeTruthy();
    expect(screen.queryByTestId(retroIds.decideSubmit)).toBeNull();

    retroApi(
      {
        [MEMBERS]: [priya],
        [DIALOG]: {
          selected: [point],
          offered: [],
          openActions: [aOpen('SHIP-958', 'Per-lane migration ranges')],
          suggestedTheme: null,
        },
      },
      { projectId: project.id, retro },
    );
    const reads = fake.callsTo(DIALOG).length;
    await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));
    await screen.findByTestId(retroDecideExistingId('SHIP-958'));
    expect(reads).toBeGreaterThan(0);
    await waitFor(() => expect(screen.queryByTestId(uiIds.errorState)).toBeNull());
  });
});

function aOpen(key: string, title: string): RetroActionRefDto {
  return { ...(actionDetail as RetroActionDetailDto).action, key, title, workItemId: `wi-${key}` };
}
