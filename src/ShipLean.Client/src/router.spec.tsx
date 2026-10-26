import { expect, test } from '@playwright/experimental-ct-react';
import { blockerIds as ids } from './ct/blockerIds';
import { DirtyScreen } from './ct/DirtyScreen';

test('Router_WhenScreenBlocksNavigation_ShouldHoldTheLeaveUntilReset', async ({ mount, page }) => {
  await mount(<DirtyScreen />, { hooksConfig: { route: '/here' } });
  await expect(page.getByTestId(ids.state)).toHaveText('unblocked');
  await page.getByTestId(ids.leave).click();
  await expect(page.getByTestId(ids.state)).toHaveText('blocked');
  await page.getByTestId(ids.stay).click();
  await expect(page.getByTestId(ids.state)).toHaveText('unblocked');
});
