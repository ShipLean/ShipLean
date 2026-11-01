import { expect, test } from '@playwright/experimental-ct-react';
import { AnalyticsHarness } from './ct/AnalyticsHarness';
import { stubAnalyticsApi } from './ct/analyticsStub';
import { analyticsTestIds } from './testIds';

test('InstanceAnalytics_WhenOpened_ShouldMountTheTab', async ({ mount, page }) => {
  await stubAnalyticsApi(page);
  await mount(<AnalyticsHarness />);
  await expect(page.getByTestId(analyticsTestIds.container)).toBeVisible();
});
