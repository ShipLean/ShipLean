import { expect, test } from '@playwright/experimental-ct-react';
import { ReconnectBanner } from './ReconnectBanner';
import { componentTestIds as ids } from './testIds';

test('ReconnectBanner_WhenReconnected_ShouldSaySo', async ({ mount, page }) => {
  await mount(<ReconnectBanner state="reconnected" changeCount={3} />);
  const banner = page.getByTestId(ids.reconnectBanner);
  await expect(banner).toHaveAttribute('data-state', 'reconnected');
  await expect(banner).toContainText('Reconnected');
  await expect(banner).toContainText('3 changes arrived');
});

test('ReconnectBanner_WhenReconnecting_ShouldOfferRetry', async ({ mount, page }) => {
  let retries = 0;
  await mount(<ReconnectBanner state="reconnecting" onRetry={() => (retries += 1)} />);
  await expect(page.getByTestId(ids.reconnectBanner)).toHaveAttribute('data-state', 'reconnecting');
  await page.getByTestId(ids.reconnectBannerRetry).click();
  expect(retries).toBe(1);
});
