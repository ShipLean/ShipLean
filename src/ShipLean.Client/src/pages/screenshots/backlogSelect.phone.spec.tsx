import { expect, test } from '@playwright/experimental-ct-react';
import { backlogView, itemRow, PROJECT, planningSprints } from '../../features/backlog/ct/fixtures';
import { SelectModeHarness } from '../../features/backlog/ct/SelectModeHarness';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { ListMode } from '../../features/backlog/list/ListMode';
import { backlogTestIds as ids } from '../../features/backlog/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';

// Select mode on a phone (SHIP-716 D3, L716g), as element shots of this task's regions only, so the sibling tasks' merges
// don't move them: the select bar, the bulk bar and its Sprint sheet. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/backlogSelect.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const CURRENT = { id: 'sp24', name: 'Sprint 24', status: 'current' } as const;
const ROWS = [
  itemRow({ key: 'SHIP-158', rank: 1, title: 'Saved cards at checkout', points: 5 }),
  itemRow({ key: 'SHIP-160', rank: 2, title: 'Biometric sign-in', points: 8 }),
  itemRow({ key: 'SHIP-171', rank: 3, title: 'Receipts by email', points: null }),
  itemRow({ key: 'SHIP-172', rank: 4, title: 'Refund status', points: 13, sprint: CURRENT }),
];

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

async function selectThree(page: Page) {
  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await page.getByTestId(touchTestIds.moreButton).first().tap();
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="select"]`)
    .tap();
  for (const key of ['SHIP-158', 'SHIP-160', 'SHIP-171']) {
    await page
      .locator(`[data-testid="${ids.row}"][data-key="${key}"]`)
      .getByTestId(ids.touchRowSelect)
      .tap();
  }
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('3 selected');
}

for (const theme of THEMES) {
  test(`BacklogSelectPhone_WhenSelecting_ShouldMatchItsScreenshot_backlogs-select_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBacklog(page, backlogView(ROWS), { sprints: planningSprints() });
    await mount(
      <SelectModeHarness>
        <ListMode project={PROJECT} />
      </SelectModeHarness>,
      route,
    );
    await selectThree(page);

    await expect(page.getByTestId(ids.selectBar)).toHaveScreenshot(`backlogs-select.${theme}.png`, {
      animations: 'disabled',
    });
    await expect(page.getByTestId(ids.bulkBar)).toHaveScreenshot(`backlogs-bulk.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BacklogSelectPhone_WhenSprintOpened_ShouldMatchItsScreenshot_backlogs-bulk-sprint_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    // Sprint 24 has no capacity of its own, so the velocity gives the bars their limit.
    await stubBacklog(
      page,
      {
        ...backlogView(ROWS),
        velocity: { window: 3, average: 21, override: null, sprints: [] },
      },
      { sprints: planningSprints() },
    );
    await mount(
      <SelectModeHarness>
        <ListMode project={PROJECT} />
      </SelectModeHarness>,
      route,
    );
    await selectThree(page);
    await page.getByTestId(ids.bulkTouchSprint).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.bulkMoveSprint)).toHaveCount(3);

    await expect(sheet).toHaveScreenshot(`backlogs-bulk-sprint.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
