import { expect, test } from '@playwright/experimental-ct-react';
import { backlogView, itemRow, PROJECT, review } from '../../features/backlog/ct/fixtures';
import { RowActionsHarness } from '../../features/backlog/ct/RowActionsHarness';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { ListMode } from '../../features/backlog/list/ListMode';
import { backlogTestIds as ids } from '../../features/backlog/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';

// The Stories rows' ⋯ sheet, the Points sheet and the not-estimated queue on a phone (SHIP-716 D9, L716f), as element
// shots of this task's regions only, so the sibling tasks' merges don't move them. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/backlogRowActions.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const ROWS = [
  itemRow({ key: 'SHIP-158', rank: 1, title: 'Saved cards at checkout', points: 5 }),
  itemRow({ key: 'SHIP-160', rank: 2, title: 'Biometric sign-in', points: null }),
  itemRow({ key: 'SHIP-171', rank: 3, title: 'Receipts by email', points: null }),
];
const SPRINTS = [
  {
    id: 'sp25',
    name: 'Sprint 25',
    startDate: '2026-11-03',
    endDate: '2026-11-16',
    goal: '',
    status: 'future' as const,
    rolloverStatus: null,
    datesLabel: '3 Nov – 16 Nov',
    timeLabel: '',
  },
];

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

for (const theme of THEMES) {
  test(`BacklogRowActionsPhone_WhenMoreOpened_ShouldMatchItsScreenshot_backlogs-row-more_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBacklog(page, backlogView(ROWS), { sprints: SPRINTS });
    await mount(<RowActionsHarness project={PROJECT} />, route);
    await expect(page.getByTestId(ids.row)).toHaveCount(3);
    await page.getByTestId(ids.row).nth(1).getByTestId(touchTestIds.moreButton).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(7);

    await expect(sheet).toHaveScreenshot(`backlogs-row-more.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BacklogRowActionsPhone_WhenPointsOpened_ShouldMatchItsScreenshot_backlogs-points_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBacklog(page, backlogView(ROWS));
    await mount(<RowActionsHarness project={PROJECT} />, route);
    await expect(page.getByTestId(ids.row)).toHaveCount(3);
    await page.getByTestId(ids.row).nth(1).getByTestId(ids.points).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Points · SHIP-160');

    await expect(sheet).toHaveScreenshot(`backlogs-points.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BacklogRowActionsPhone_WhenQueueOpened_ShouldMatchItsScreenshot_backlogs-queue_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBacklog(page, {
      ...backlogView(ROWS),
      review: review({ unestimated: [ROWS[1].id, ROWS[2].id] }),
    });
    await mount(<ListMode project={PROJECT} />, route);
    await page.getByTestId(ids.planCheck).and(page.locator('[data-check="unestimated"]')).tap();
    const banner = page.getByTestId(ids.queueBanner);
    await expect(banner.getByTestId(ids.queueStepKey)).toHaveText('SHIP-160');

    await expect(banner).toHaveScreenshot(`backlogs-queue.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
