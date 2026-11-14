import { expect, test } from '@playwright/experimental-ct-react';
import type { WorkItemRetrosDto } from '@shiplean/core/api/retros';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { stubRetros, workItemRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { WorkItemRetroSection } from './WorkItemRetroSection';

// The item's Retrospectives rows on a phone (SHIP-729 R8): one two-line row link per retro. Regenerate the baselines with
// scripts/update-page-shots.sh src/features/retros/workItem/WorkItemRetro.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const props = { projectId: 'p1', projectKey: 'SHIP', itemKey: 'SHIP-141', workItemId: 'w1' };
const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

const [agent, person] = workItemRetros.rows;
const partialPerson = {
  ...person,
  retroId: '00000012-0000-4000-8001-000000000399',
  partial: true,
  totals: {
    counts: { wentWell: 2, notInPlan: 0, wentBadly: 1, improve: 3 },
    toDecide: 4,
    decided: 0,
    decidable: 4,
    actions: 0,
  },
};
const rows: WorkItemRetrosDto = { ...workItemRetros, rows: [agent, partialPerson, person] };

async function serve(page: Parameters<typeof stubRetros>[0]) {
  await stubRetros(page);
  await page.route('**/api/work-items/w1/retros', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(rows) }),
  );
}

for (const theme of THEMES) {
  test(`WorkItemRetroPhone_WhenRendered_ShouldMatchItsScreenshot_work-item-retros-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page);
    await mount(<WorkItemRetroSection {...props} />, route);
    const section = page.getByTestId(ids.workItemRetroSection);
    await expect(section.getByTestId(ids.workItemRetroRow)).toHaveCount(3);
    await expect(section).toHaveScreenshot(`work-item-retros-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('WorkItemRetroPhone_WhenRendered_ShouldShowTwoLineRowLinksWithoutATable', async ({
  mount,
  page,
}) => {
  await serve(page);
  await mount(<WorkItemRetroSection {...props} />, route);
  const section = page.getByTestId(ids.workItemRetroSection);
  const phoneRows = section.getByTestId(ids.workItemRetroRow);
  await expect(phoneRows).toHaveCount(3);
  await expect(section.locator('table')).toHaveCount(0);
  for (const [index, row] of rows.rows.entries()) {
    const link = phoneRows.nth(index);
    await expect(link).toHaveAttribute(
      'href',
      `/p/SHIP/retros/items/SHIP-141?retro=${row.retroId}`,
    );
    expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(32);
    await expect(link.getByTestId(shared.retroAuthorChip)).toHaveCount(1);
    await expect(link.getByTestId(ids.retroCategoryCount)).toHaveCount(4);
    await expect(link).toContainText(row.createdAtLabel);
  }
  await expect(phoneRows.nth(1)).toContainText('partial');
  await expect(phoneRows.nth(0)).not.toContainText('partial');
  await expect(phoneRows.nth(1)).toContainText('4 to decide');
  await expect(phoneRows.nth(2).locator('[data-to-decide]')).toHaveCount(0);
  await expect(phoneRows.nth(2)).toContainText('nothing to decide');
});

test('WorkItemRetroPhone_WhenRendered_ShouldNotScrollSideways', async ({ mount, page }) => {
  await serve(page);
  await mount(<WorkItemRetroSection {...props} />, route);
  const section = page.getByTestId(ids.workItemRetroSection);
  await expect(section.getByTestId(ids.workItemRetroRow).first()).toBeVisible();
  const fits = await section.evaluate((el) => el.scrollWidth <= el.clientWidth);
  expect(fits).toBe(true);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
  const link = section.getByRole('link', { name: 'All retros for this item' });
  expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(32);
});
