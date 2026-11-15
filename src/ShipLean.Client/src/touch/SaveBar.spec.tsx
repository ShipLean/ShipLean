import { expect, test } from '@playwright/experimental-ct-react';
import { SaveBarHarness } from './ct/SaveBarHarness';
import { saveBarHarnessIds as harness } from './ct/saveBarHarnessIds';
import { touchTestIds as ids } from './testIds';

// Above 900px pages keep their own Save and Cancel (K5): the touch Save bar renders nothing, not even its spacer.
test('SaveBar_WhenAbove900_ShouldRenderNothing', async ({ mount, page }) => {
  await mount(<SaveBarHarness />);
  await expect(page.getByTestId(harness.ctSaveBarPageEnd)).toBeAttached();
  await expect(page.getByTestId(ids.saveBar)).toHaveCount(0);
  await expect(page.getByTestId(ids.saveBarSave)).toHaveCount(0);
  await expect(page.locator('[aria-hidden="true"]')).toHaveCount(0);
});
