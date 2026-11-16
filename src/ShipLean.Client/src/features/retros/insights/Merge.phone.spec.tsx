import { expect, test } from '@playwright/experimental-ct-react';
import { retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { RetroInsightsPanel } from './RetroInsightsPanel';

// Proposed merge, Unthemed and What keeps working on a phone (SHIP-731 D8), as element shots of this task's regions only.
// Regenerate with scripts/update-page-shots.sh src/features/retros/insights/Merge.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const open = { hooksConfig: { route: '/p/SHIP/retros/insights?view=themes&range=6' } };

for (const colour of THEMES) {
  test(`MergeProposal_WhenOnAPhone_ShouldMatchItsScreenshot_retro-insights-proposal-phone_${colour}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<RetroInsightsPanel project={insightsProject} />, open);
    const proposal = page.getByRole('region', { name: 'Proposed merges' });
    await expect(proposal).toBeVisible();
    await expect(proposal).toHaveScreenshot(`retro-insights-proposal-phone.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`Unthemed_WhenOnAPhone_ShouldMatchItsScreenshot_retro-insights-unthemed-phone_${colour}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<RetroInsightsPanel project={insightsProject} />, open);
    const unthemed = page.getByTestId(ids.retroInsightsUnthemed);
    await expect(unthemed).toBeVisible();
    await expect(unthemed).toContainText('Give them themes');
    await expect(unthemed).toHaveScreenshot(`retro-insights-unthemed-phone.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`KeepsWorking_WhenOnAPhone_ShouldMatchItsScreenshot_retro-insights-keeps-working-phone_${colour}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<RetroInsightsPanel project={insightsProject} />, open);
    const keeps = page.getByTestId(ids.retroKeepsWorking);
    await expect(keeps).toBeVisible();
    await expect(keeps.locator('li')).toHaveCount(retroInsights.keepsWorking.length);
    await expect(keeps).toHaveScreenshot(`retro-insights-keeps-working-phone.${colour}.png`, {
      animations: 'disabled',
    });
  });
}

const merge = { hooksConfig: { route: '/p/SHIP/retros/insights?view=themes&merge=' } };

test('InsightsMerge_Checkboxes_ShouldBeTouchSized', async ({ mount, page }) => {
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
  await stubRetros(page);
  await mount(<RetroInsightsPanel project={insightsProject} />, merge);
  const boxes = page.getByTestId(ids.retroMergeSource);
  await expect(boxes.first()).toBeVisible();
  expect(await boxes.count()).toBeGreaterThan(1);
  for (const box of await boxes.all()) {
    const size = (await box.boundingBox())!;
    expect(size.width).toBeGreaterThanOrEqual(20);
    expect(size.height).toBeGreaterThanOrEqual(20);
  }
});

test('InsightsMerge_DisabledMerge_ShouldStillLookLikeAButton', async ({ mount, page }) => {
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
  await stubRetros(page);
  await mount(<RetroInsightsPanel project={insightsProject} />, merge);
  const save = page.getByTestId(ids.retroMergeSave);
  await expect(save).toBeVisible();
  await expect(save).toBeDisabled();
  await expect(save).toHaveCSS('border-top-width', '1px');
  const background = await save.evaluate((el) => getComputedStyle(el).backgroundColor);
  expect(background).not.toBe('rgba(0, 0, 0, 0)');
  expect((await save.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  // Ticking a source and choosing Into enables it.
  await page.getByTestId(ids.retroMergeSource).first().check();
  await page.getByTestId(ids.retroMergeTarget).selectOption({ index: 2 });
  await expect(save).toBeEnabled();
});
