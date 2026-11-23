import { expect, test } from '@playwright/experimental-ct-react';
import { timelineTestIds as ids } from '../testIds';
import { TimelineHeader } from '../TimelineHeader';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

test('TimelineHeader_ShouldShowKeyTitleStateAndEpic', async ({ mount, page }) => {
  await mount(<TimelineHeader view={FINISHED_TIMELINE} projectKey="SHIP" />);
  await expect(page.getByTestId(ids.header)).toContainText('SHIP-99');
  await expect(page.getByTestId(ids.headerTitle)).toHaveText('Coupons v2');
  await expect(page.getByTestId(ids.headerState)).toContainText('Done');
  await expect(page.getByTestId(ids.headerEpic)).toContainText('Checkout conversion');
  await expect(page.getByTestId(ids.keyDates)).toHaveAttribute('data-compact', 'false');
});

test('TimelineHeader_BackLink_ShouldOpenTheFeature', async ({ mount, page }) => {
  await mount(<TimelineHeader view={FINISHED_TIMELINE} projectKey="SHIP" />);
  await expect(page.getByTestId(ids.headerBack)).toHaveAttribute('href', '/p/SHIP/items/SHIP-99');
});

test('TimelineHeader_Finished_ShouldShowCaptionAndFourKpis', async ({ mount, page }) => {
  await mount(<TimelineHeader view={FINISHED_TIMELINE} projectKey="SHIP" />);
  await expect(page.getByTestId(ids.headerCaption)).toHaveText(
    "Finished Wed 21 Oct. Bars run from Active to Resolved, from each task's state history.",
  );
  const kpis = page.getByTestId(ids.kpi);
  await expect(kpis).toHaveCount(4);
  await expect(kpis.nth(0)).toContainText('64');
  await expect(kpis.nth(0)).toContainText('Lead time, Added → Finished');
  await expect(kpis.nth(1)).toContainText('Execution');
  await expect(kpis.nth(2)).toContainText('8 of 9 tasks');
  await expect(kpis.nth(2)).toContainText('started · 1 with no start');
  await expect(kpis.nth(3)).toContainText('9 reviews');
  await expect(kpis.nth(3)).toContainText('1 asked for changes');
});

test('TimelineHeader_Running_ShouldShowCaptionAndFourKpis', async ({ mount, page }) => {
  await mount(<TimelineHeader view={RUNNING_TIMELINE} projectKey="SHIP" />);
  await expect(page.getByTestId(ids.headerCaption)).toHaveText(
    'Still running. Open bars run to now, Sat 24 Oct, 14:00.',
  );
  const kpis = page.getByTestId(ids.kpi);
  await expect(kpis).toHaveCount(4);
  await expect(kpis.nth(0)).toContainText('Lead time, Added → now');
  await expect(kpis.nth(1)).toContainText('32.2');
  await expect(kpis.nth(1)).toContainText('Execution so far');
  await expect(kpis.nth(2)).toContainText('4 of 5 tasks');
  await expect(kpis.nth(2)).toContainText('1 not started');
  await expect(kpis.nth(3)).toContainText('all approved first time');
});
