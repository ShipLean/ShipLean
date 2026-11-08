import { expect, test } from '@playwright/experimental-ct-react';
import { ToolbarHarness } from '../ct/ToolbarHarness';
import { backlogTestIds as ids } from '../testIds';

test('TriageChip_WhenCount_ShouldLinkToFilter', async ({ mount, page }) => {
  await mount(<ToolbarHarness triageCount={3} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  const chip = page.getByTestId(ids.triageChip);
  await expect(chip).toHaveText('3 in triage');

  await chip.click();
  await expect(page.getByTestId('harness-state')).toContainText('triage-shown');
});

test('TriageChip_WhenNoCount_ShouldNotShow', async ({ mount, page }) => {
  await mount(<ToolbarHarness triageCount={0} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.triageChip)).toHaveCount(0);
});
