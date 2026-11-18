import { expect, test } from '@playwright/experimental-ct-react';
import feature from '../../../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.feature.json' with { type: 'json' };
import { AppRoutes } from '../../../AppRoutes';
import { stubProjectApi } from '../../../pages/pagesFixtures';
import { stubInspection } from '../../inspection/ct/inspectionMocks';
import { inspectionTestIds } from '../../inspection/testIds';
import { pagesTestIds } from '../../pages/testIds';
import { timelineTestIds as ids } from '../testIds';
import { stubFeatureTimeline } from './stubFeatureTimeline';

const path = '/p/SHIP/items/SHIP-99/timeline';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

/** The Feature's own item read, which carries the project's Inspection flag, and every inspection route. */
async function stubFeatureItem(page: Page, inspectionEnabled: boolean) {
  await page.route('**/work-items/by-key/SHIP-99', (route) =>
    route.fulfill({ json: { ...feature, inspectionEnabled } }),
  );
  return stubInspection(page);
}

test('FeatureTimelineSlot_WhenOpenedFromFeature_ShouldPostOpenedWithFromFeature', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  const server = await stubFeatureTimeline(page, { fixture: 'finished' });
  await mount(<AppRoutes />, { hooksConfig: { route: `${path}?from=feature` } });
  await expect(page.getByTestId(ids.chart)).toBeVisible();

  await expect.poll(() => server.usageSent().length).toBe(1);
  const [sent] = server.usageSent();
  expect(sent.path).toBe('usage/timeline-opened');
  expect(JSON.parse(sent.body ?? '{}')).toEqual({ from: 'feature' });
});

test('FeatureTimelineSlot_WhenOpenedFromLink_ShouldPostFromLink', async ({ mount, page }) => {
  await stubProjectApi(page);
  const server = await stubFeatureTimeline(page, { fixture: 'finished' });
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  await expect(page.getByTestId(ids.chart)).toBeVisible();

  await expect.poll(() => server.usageSent().length).toBe(1);
  expect(JSON.parse(server.usageSent()[0].body ?? '{}')).toEqual({ from: 'link' });
});

test('FeatureTimelineSlot_WhenBarClicked_ShouldWriteSelectAndPostBarSelected', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  const server = await stubFeatureTimeline(page, { fixture: 'finished' });
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  await expect.poll(() => server.usageSent().length).toBe(1);

  await page.locator(`[data-testid="${ids.chartBar}"][data-key="SHIP-192"]`).click();
  await expect(page.getByTestId(ids.rail)).toHaveAttribute('data-selected', 'SHIP-192');
  await expect
    .poll(() => server.usageSent().map((s) => s.path))
    .toEqual(['usage/timeline-opened', 'usage/timeline-bar-selected']);
});

test('FeatureTimelineSlot_WhenNotFound_ShouldShowNotFound', async ({ mount, page }) => {
  await stubProjectApi(page);
  const server = await stubFeatureTimeline(page, { fixture: 'notFound' });
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
  await expect(page.getByTestId(ids.loadFailed)).toContainText(/not found/i);
  expect(server.usageSent()).toEqual([]);
});

test('FeatureTimelineSlot_WhenError_ShouldShowError', async ({ mount, page }) => {
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'error' });
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
  await expect(page.getByTestId(ids.loadFailed)).not.toContainText(/not found/i);
});

test('FeatureTimelineSlot_WhenLoading_ShouldShowSkeleton', async ({ mount, page }) => {
  await stubProjectApi(page);
  await page.route('**/api/projects/*/work-items/by-key/*/timeline', () => new Promise(() => {}));
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  await expect(page.getByTestId(ids.loading)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.slotFeatureTimeline)).toHaveCount(0);
});

test('FeatureTimelineSlot_WhenInspectionIsOn_ShouldListTheSelectedTasksAgentRuns', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'finished' });
  const inspection = await stubFeatureItem(page, true);
  await mount(<AppRoutes />, { hooksConfig: { route: path } });
  await expect(page.getByTestId(ids.chart)).toBeVisible();
  // Nothing selected: the rail asks the trace store nothing.
  expect(inspection.urls).toEqual([]);

  await page.locator(`[data-testid="${ids.chartBar}"][data-key="SHIP-192"]`).click();
  const runs = page.getByTestId(ids.rail).getByTestId(inspectionTestIds.railAgentRunsRow);
  await expect(runs.first().getByRole('link')).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-192/runs/sess-cc-1',
  );
  expect(inspection.urls.filter((u) => u.endsWith('/items/SHIP-192/runs'))).toHaveLength(1);
});

test('FeatureTimelineSlot_WhenInspectionIsOff_ShouldShowNoAgentRunsAndRequestNone', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'finished' });
  const inspection = await stubFeatureItem(page, false);
  await mount(<AppRoutes />, { hooksConfig: { route: `${path}?select=SHIP-192` } });

  await expect(page.getByTestId(ids.railSelected)).toBeVisible();
  await expect(page.getByTestId(inspectionTestIds.railAgentRuns)).toHaveCount(0);
  expect(inspection.urls).toEqual([]);
});
