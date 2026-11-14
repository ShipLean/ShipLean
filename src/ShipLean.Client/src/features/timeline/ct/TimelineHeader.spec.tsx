import { expect, test } from '@playwright/experimental-ct-react';
import { timelineTestIds as ids } from '../testIds';
import { TimelineHeader } from '../TimelineHeader';
import { FINISHED_TIMELINE } from './stubFeatureTimeline';

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
