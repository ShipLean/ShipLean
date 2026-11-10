import type { RetroFeatureRowDto, RetroInsightsDto } from '@shiplean/core/api/retros';
import { fireEvent, screen, within } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';
import { links } from '../../links';
import { aProject } from '../../test-support/builders';
import { apiError, restoreFetch } from '../../test-support/fakeApi';
import { designInsights, designRetroFeatures, retroApi } from '../../test-support/retro';
import { renderScreen } from '../../test-support/renderScreen';
import type { Scheme } from '../../theme/themes';
import { uiIds } from '../../ui/testIds';
import {
  retroFeatureBarId,
  retroFeatureLastId,
  retroFeatureNoRetroId,
  retroFeaturePartId,
  retroFeatureRowId,
  retroFeatureToDecideId,
  retroFeaturesIds,
  retroHeatActionId,
  retroHeatCellId,
  retroHeatFixId,
  retroHeatId,
  retroHeatNoActionId,
  retroIds,
} from '../testIds';
import { RetroFeaturesView } from './RetroFeaturesView';

const mockPush = jest.fn();
jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, dismissTo: jest.fn() }),
  useLocalSearchParams: () => ({ key: 'SHIP' }),
}));

afterEach(restoreFetch);
beforeEach(() => mockPush.mockClear());

const project = aProject();
const INSIGHTS = `GET /api/projects/${project.id}/retros/insights`;
const FEATURES = `GET /api/projects/${project.id}/retros/features`;

async function renderView(
  scheme: Scheme,
  table: Parameters<typeof retroApi>[0] = {},
  opts: { insights?: RetroInsightsDto; features?: ReturnType<typeof designRetroFeatures> } = {},
) {
  const insights = opts.insights ?? designInsights();
  const features = opts.features ?? designRetroFeatures();
  const fake = retroApi(table, { projectId: project.id, insights, features });
  const rendered = await renderScreen(<RetroFeaturesView projectId={project.id} />, { scheme });
  return { ...rendered, fake, insights, features };
}

// A read that fails once, then answers with `then`.
const failsOnce = (then: object) => {
  let calls = 0;
  return () => (calls++ === 0 ? apiError(500, 'internal') : then);
};

const background = (id: string) =>
  StyleSheet.flatten(screen.getByTestId(id).props.style).backgroundColor as string;
// The alpha byte of a #rrggbbaa colour.
const alpha = (hex: string) => parseInt(hex.slice(7, 9), 16);

describe.each<Scheme>(['light', 'dark'])('Retro Features view (%s)', (scheme) => {
  it('renders the features state: recurring strips over six sprints, then Features with retros', async () => {
    const { insights, features } = await renderView(scheme);
    await screen.findByTestId(retroFeatureRowId('SHIP-101'));

    expect(screen.getByText('Recurring themes')).toBeTruthy();
    expect(screen.getByText('Features with retros')).toBeTruthy();
    // One card per recurring theme, six cells each, the first and last column labels under them.
    expect(insights.themes).toHaveLength(3);
    for (const t of insights.themes) {
      const card = screen.getByTestId(retroHeatId(t.theme.id));
      expect(within(card).getByText(t.theme.name)).toBeTruthy();
      expect(within(card).getAllByTestId(/-cell-\d$/)).toHaveLength(6);
      expect(within(card).getByText('Sprint 19')).toBeTruthy();
      expect(within(card).getByText('Sprint 24')).toBeTruthy();
    }
    // Action items by key and state; a theme with none says so.
    const baselines = insights.themes[0];
    expect(
      within(screen.getByTestId(retroHeatActionId(baselines.theme.id, 'SHIP-188'))).getByText(
        'SHIP-188',
      ),
    ).toBeTruthy();
    expect(screen.getByTestId(retroHeatActionId(baselines.theme.id, 'SHIP-133'))).toBeTruthy();
    // The count: three recurring themes.
    expect(screen.getByTestId(retroFeaturesIds.recurringCount)).toHaveTextContent('3');

    // The Features with their legend, then each row in the server's order.
    expect(screen.getByTestId(retroFeaturesIds.legend)).toBeTruthy();
    for (const label of ['Went well', 'Not in the plan', 'Went badly', 'Can be improved'])
      expect(within(screen.getByTestId(retroFeaturesIds.legend)).getByText(label)).toBeTruthy();
    const keys = screen
      .getAllByTestId(/^retro-features-row-SHIP-\d+$/)
      .map((n) => n.props.testID as string);
    expect(keys).toEqual(features.features.map((f) => retroFeatureRowId(f.item.key)));
    const first = screen.getByTestId(retroFeatureRowId('SHIP-101'));
    expect(within(first).getByText('Checkout v2')).toBeTruthy();
    expect(within(first).getByText('Feature')).toBeTruthy();
    expect(within(first).getByText('4 · 2 · 5 · 4')).toBeTruthy();
    expect(within(first).getByText('4 retros · 3 action items')).toBeTruthy();
    expect(screen.getByTestId(retroFeatureLastId('SHIP-101'))).toHaveTextContent('2 days ago');
    // The bar splits in the web's order.
    const bar = screen.getByTestId(retroFeatureBarId('SHIP-101'));
    expect(
      within(bar)
        .getAllByTestId(/-part-/)
        .map((n) => n.props.testID),
    ).toEqual(
      ['wentWell', 'notInPlan', 'wentBadly', 'improve'].map((c) =>
        retroFeaturePartId('SHIP-101', c),
      ),
    );
    expect(
      StyleSheet.flatten(
        screen.getByTestId(retroFeaturePartId('SHIP-101', 'wentBadly')).props.style,
      ).flex,
    ).toBe(5);
  });

  it('shades cells by count against the largest and marks a closed fix', async () => {
    const { insights } = await renderView(scheme);
    await screen.findByTestId(retroHeatId(insights.themes[0].theme.id));
    const [baselines, conflicts] = insights.themes;
    // The largest count is 3 (baselines, column 3); a cell of 3 is the strongest, 2 weaker, 1 weaker still, 0 unshaded.
    const strongest = background(retroHeatCellId(baselines.theme.id, 3));
    const two = background(retroHeatCellId(baselines.theme.id, 1));
    const one = background(retroHeatCellId(baselines.theme.id, 0));
    expect(alpha(strongest)).toBe(Math.round(0.7 * 255));
    expect(alpha(two)).toBe(Math.round(0.47 * 255));
    expect(alpha(one)).toBe(Math.round(0.23 * 255));
    expect(alpha(strongest)).toBeGreaterThan(alpha(two));
    expect(alpha(two)).toBeGreaterThan(alpha(one));
    // The same count in another theme shades the same: the largest is over all the themes.
    expect(background(retroHeatCellId(conflicts.theme.id, 5))).toBe(strongest);
    expect(background(retroHeatCellId(conflicts.theme.id, 0)).length).toBeLessThan(9);
    // The count shows in the cell.
    expect(screen.getByTestId(retroHeatCellId(baselines.theme.id, 3))).toHaveTextContent('3');
    expect(screen.getByTestId(retroHeatCellId(conflicts.theme.id, 0))).toHaveTextContent('');

    // The fix closed in column 4 of the baselines theme only.
    expect(screen.getByTestId(retroHeatFixId(baselines.theme.id, 4))).toBeTruthy();
    expect(screen.getAllByTestId(/-fix-\d$/)).toHaveLength(1);
  });

  it('shows to decide and No Feature retro yet', async () => {
    const features = designRetroFeatures();
    const saved = features.features[3];
    features.features[3] = { ...saved, totals: { ...saved.totals, toDecide: 0 } };
    await renderView(scheme, {}, { features });
    await screen.findByTestId(retroFeatureRowId('SHIP-101'));

    expect(screen.getByTestId(retroFeatureToDecideId('SHIP-101'))).toHaveTextContent('6 to decide');
    expect(screen.queryByTestId(retroFeatureToDecideId('SHIP-104'))).toBeNull();
    expect(screen.getByTestId(retroFeatureNoRetroId('SHIP-102'))).toHaveTextContent(
      'No Feature retro yet',
    );
    expect(screen.getByTestId(retroFeatureNoRetroId('SHIP-108'))).toBeTruthy();
    expect(screen.queryByTestId(retroFeatureNoRetroId('SHIP-101'))).toBeNull();
  });

  it('lists loose stories and bugs after the Features', async () => {
    const features = designRetroFeatures();
    const loose: RetroFeatureRowDto = {
      ...features.features[0],
      item: {
        ...features.features[0].item,
        id: 'loose-1',
        key: 'SHIP-300',
        title: 'Fix the receipt font',
        type: { id: 'bug', name: 'Bug' } as RetroFeatureRowDto['item']['type'],
      },
    };
    await renderView(scheme, {}, { features: { ...features, loose: [loose] } });
    await screen.findByTestId(retroFeatureRowId('SHIP-300'));
    const keys = screen
      .getAllByTestId(/^retro-features-row-SHIP-\d+$/)
      .map((n) => n.props.testID as string);
    expect(keys.at(-1)).toBe(retroFeatureRowId('SHIP-300'));
    expect(keys).toHaveLength(6);
  });

  it('opens an action item and a Feature on tap', async () => {
    const { insights } = await renderView(scheme);
    await fireEvent.press(
      await screen.findByTestId(retroHeatActionId(insights.themes[0].theme.id, 'SHIP-188')),
    );
    expect(mockPush).toHaveBeenLastCalledWith(links.item('SHIP', 'SHIP-188'));
    await fireEvent.press(screen.getByTestId(retroFeatureRowId('SHIP-103')));
    expect(mockPush).toHaveBeenLastCalledWith(links.item('SHIP', 'SHIP-103'));
    expect(mockPush).toHaveBeenCalledTimes(2);
  });

  it('says No action item yet for a theme with none', async () => {
    const insights = designInsights();
    insights.themes[1] = { ...insights.themes[1], actionItems: [] };
    await renderView(scheme, {}, { insights });
    expect(
      await screen.findByTestId(retroHeatNoActionId(insights.themes[1].theme.id)),
    ).toHaveTextContent('No action item yet');
    expect(screen.queryByTestId(retroHeatNoActionId(insights.themes[0].theme.id))).toBeNull();
  });

  it("shows each read's error on its own", async () => {
    const insights = designInsights();
    const { fake } = await renderView(scheme, { [INSIGHTS]: failsOnce(insights) }, { insights });
    const recurringError = await screen.findByTestId(retroFeaturesIds.recurringError);
    expect(within(recurringError).getByText("Couldn't load the recurring themes.")).toBeTruthy();
    // The Features read is fine.
    expect(await screen.findByTestId(retroFeatureRowId('SHIP-101'))).toBeTruthy();
    expect(screen.queryByTestId(retroFeaturesIds.listError)).toBeNull();

    // Retry reads only the insights again, and the themes come back.
    const features = fake.callsTo(FEATURES).length;
    await fireEvent.press(within(recurringError).getByTestId(uiIds.errorStateAction));
    expect(await screen.findByTestId(retroHeatId(insights.themes[0].theme.id))).toBeTruthy();
    expect(screen.queryByTestId(retroFeaturesIds.recurringError)).toBeNull();
    expect(fake.callsTo(INSIGHTS)).toHaveLength(2);
    expect(fake.callsTo(FEATURES)).toHaveLength(features);
  });

  it("shows the Features read's error and retries it on its own", async () => {
    const features = designRetroFeatures();
    const { fake } = await renderView(scheme, { [FEATURES]: failsOnce(features) }, { features });
    const listError = await screen.findByTestId(retroFeaturesIds.listError);
    expect(within(listError).getByText("Couldn't load the Features with retros.")).toBeTruthy();
    expect(await screen.findByText('Recurring themes')).toBeTruthy();
    expect(screen.queryByTestId(retroFeaturesIds.recurringError)).toBeNull();
    expect(await screen.findAllByTestId(/^retro-features-theme-[0-9a-f-]+$/)).toHaveLength(3);

    const insights = fake.callsTo(INSIGHTS).length;
    await fireEvent.press(within(listError).getByTestId(uiIds.errorStateAction));
    expect(await screen.findByTestId(retroFeatureRowId('SHIP-101'))).toBeTruthy();
    expect(screen.queryByTestId(retroFeaturesIds.listError)).toBeNull();
    expect(fake.callsTo(FEATURES)).toHaveLength(2);
    expect(fake.callsTo(INSIGHTS)).toHaveLength(insights);
  });

  it('shows both empty texts', async () => {
    const insights = designInsights();
    insights.themes = insights.themes.map((t) => ({ ...t, status: 'resolved' as const }));
    await renderView(scheme, {}, { insights, features: { features: [], loose: [] } });
    expect(
      within(await screen.findByTestId(retroFeaturesIds.recurringEmpty)).getByText(
        'No recurring themes in the last six sprints.',
      ),
    ).toBeTruthy();
    expect(
      within(await screen.findByTestId(retroFeaturesIds.listEmpty)).getByText(
        'No Features have retros yet.',
      ),
    ).toBeTruthy();
    expect(screen.queryByTestId(retroFeaturesIds.legend)).toBeNull();
  });

  it('reads insights with range 6', async () => {
    const { fake } = await renderView(scheme);
    await screen.findByTestId(retroFeatureRowId('SHIP-101'));
    const calls = fake.callsTo(INSIGHTS);
    expect(calls).toHaveLength(1);
    expect(new URL(calls[0].url, 'https://shiplean.test').searchParams.get('range')).toBe('6');
    expect(screen.getByTestId(retroIds.features)).toBeTruthy();
  });
});
