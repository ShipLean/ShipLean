import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubProjectApi } from '../../../pages/pagesFixtures';
import { pagesTestIds } from '../../pages/testIds';
import { timelineTestIds as ids } from '../testIds';
import { stubFeatureTimeline } from './stubFeatureTimeline';

const path = '/p/SHIP/items/SHIP-99/timeline';

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
