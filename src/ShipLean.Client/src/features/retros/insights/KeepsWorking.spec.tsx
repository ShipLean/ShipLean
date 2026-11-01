import { expect, test } from '@playwright/experimental-ct-react';
import { retroInsights } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { KeepsWorking } from './KeepsWorking';

test('KeepsWorking_WhenMounted_ShouldListTheGoodThemesOnly', async ({ mount, page }) => {
  await mount(<KeepsWorking project={insightsProject} insights={retroInsights} />);
  const section = page.getByTestId(ids.retroKeepsWorking);
  const rows = section.locator('[data-theme-id]');
  await expect(rows).toHaveCount(retroInsights.keepsWorking.length);
  for (const row of retroInsights.keepsWorking) {
    expect(row.theme.tone).toBe('good');
    const line = section.locator(`[data-theme-id="${row.theme.id}"]`);
    await expect(line).toContainText(row.theme.name);
    await expect(line).toContainText(String(row.total));
    await expect(line).toContainText(row.latest ?? '');
  }
  for (const bad of retroInsights.themes) {
    await expect(section).not.toContainText(bad.theme.name);
  }
});
