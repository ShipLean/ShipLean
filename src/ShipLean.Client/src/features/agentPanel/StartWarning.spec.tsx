import { expect, test } from '@playwright/experimental-ct-react';
import { StartWarning } from './StartWarning';
import { agentPanelTestIds as ids } from './testIds';
import { canStart, canStartWithWarning } from './ct/fixtures';

test('StartWarning_WhenTheLastDeliveryFailed_ShouldShowTheWarningText', async ({ mount, page }) => {
  await mount(<StartWarning availability={canStartWithWarning} />);

  await expect(page.getByTestId(ids.startWarning)).toHaveText(
    'The last delivery to this connection failed 10 minutes ago.',
  );
});

test('StartWarning_WhenThereIsNoWarning_ShouldRenderNothing', async ({ mount, page }) => {
  await mount(<StartWarning availability={canStart} />);

  await expect(page.getByTestId(ids.startWarning)).toHaveCount(0);
  expect(await page.locator('#root').innerHTML()).toBe('');
});
