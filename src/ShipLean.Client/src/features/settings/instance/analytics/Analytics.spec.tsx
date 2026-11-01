import { expect, test } from '@playwright/experimental-ct-react';
import { AnalyticsHarness } from './ct/AnalyticsHarness';
import { analyticsTestIds } from './testIds';

test('InstanceAnalytics_WhenOpened_ShouldMountTheTab', async ({ mount, page }) => {
  await mount(<AnalyticsHarness />);
  await expect(page.getByTestId(analyticsTestIds.container)).toBeVisible();
});
