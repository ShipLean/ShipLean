import { expect, test } from '@playwright/experimental-ct-react';
import { assignChildren, assignMembers } from '../../features/board/cards/assignTouchFixtures';
import { BoardHarness } from '../../features/board/ct/BoardHarness';
import { rollupBoard, storiesBoard } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardTestIds as ids } from '../../features/board/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';

// The card's actions sheet, the Assign sheet and the children list on a phone (SHIP-719, L719e), as element shots of this task's regions only.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/boardAssign.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const stories = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const features = { hooksConfig: { route: '/p/SHIP/boards/features' } };

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

for (const theme of THEMES) {
  test(`BoardAssignPhone_WhenActionsOpened_ShouldMatchItsScreenshot_board-card-actions_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, storiesBoard(), { members: assignMembers });
    await mount(<BoardHarness />, stories);
    await page
      .getByTestId(ids.card)
      .filter({ hasText: 'SHIP-160' })
      .getByTestId(touchTestIds.moreButton)
      .tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.locator('[data-action-id="assign"]')).toBeVisible();

    await expect(sheet).toHaveScreenshot(`board-card-actions.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BoardAssignPhone_WhenAssignOpened_ShouldMatchItsScreenshot_board-assign_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, storiesBoard(), { members: assignMembers });
    await mount(<BoardHarness />, stories);
    await page
      .getByTestId(ids.card)
      .filter({ hasText: 'SHIP-160' })
      .getByTestId(touchTestIds.moreButton)
      .tap();
    await page.locator('[data-action-id="assign"]').tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.assignOption)).toHaveCount(assignMembers.length + 1);

    await expect(sheet).toHaveScreenshot(`board-assign.${theme}.png`, { animations: 'disabled' });
  });

  test(`BoardAssignPhone_WhenChildrenOpened_ShouldMatchItsScreenshot_features-board-children_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, rollupBoard(), { level: 'features', children: () => assignChildren });
    await mount(<BoardHarness level="features" />, features);
    await page
      .getByTestId(ids.card)
      .filter({ hasText: 'SHIP-101' })
      .getByTestId(touchTestIds.moreButton)
      .tap();
    await page.locator('[data-action-id="below"]').tap();
    const list = page.getByTestId(ids.childList);
    await expect(list.getByTestId(ids.childRow)).toHaveCount(assignChildren.length);

    await expect(list).toHaveScreenshot(`features-board-children.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
