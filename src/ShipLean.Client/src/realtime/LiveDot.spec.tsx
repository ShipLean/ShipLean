import { expect, test } from '@playwright/experimental-ct-react';
import { realtimeTestIds as ids } from '../features/realtime/testIds';
import { LiveDot } from './LiveDot';

test('LiveDot_WhenReconnecting_ShouldSaySo', async ({ mount, page }) => {
  await mount(<LiveDot state="reconnecting" />);
  const dot = page.getByTestId(ids.liveDot);
  await expect(dot).toHaveAttribute('data-state', 'reconnecting');
  await expect(dot).toContainText('Reconnecting');
});

test('LiveDot_WhenLive_ShouldSayLive', async ({ mount, page }) => {
  await mount(<LiveDot state="live" />);
  await expect(page.getByTestId(ids.liveDot)).toContainText('Live');
});

test('LiveDot_WhenOffline_ShouldSaySo', async ({ mount, page }) => {
  await mount(<LiveDot state="offline" />);
  await expect(page.getByTestId(ids.liveDot)).toContainText('Offline');
});
