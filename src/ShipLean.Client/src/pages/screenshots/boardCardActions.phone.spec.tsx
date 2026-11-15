import { expect, test } from '@playwright/experimental-ct-react';
import { BoardHarness } from '../../features/board/ct/BoardHarness';
import { storiesBoard } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardTestIds as ids } from '../../features/board/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';

// A card with its ⋯ and the Move to sheet on a phone (SHIP-719, L719c), as element shots of this task's regions only.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/boardCardActions.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

for (const theme of THEMES) {
  test(`BoardCardActionsPhone_WhenOpened_ShouldMatchItsScreenshot_board-card_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, storiesBoard());
    await mount(<BoardHarness />, route);
    const card = page.getByTestId(ids.card).filter({ hasText: 'SHIP-160' });
    await expect(card.getByTestId(touchTestIds.moreButton)).toBeVisible();

    await expect(card).toHaveScreenshot(`board-card.${theme}.png`, { animations: 'disabled' });
  });

  test(`BoardCardActionsPhone_WhenMoveToOpened_ShouldMatchItsScreenshot_board-move-to_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, storiesBoard());
    await mount(<BoardHarness />, route);
    await page
      .getByTestId(ids.card)
      .filter({ hasText: 'SHIP-160' })
      .getByTestId(touchTestIds.moreButton)
      .tap();
    await page.locator('[data-action-id="move-to"]').tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Move to');

    await expect(sheet).toHaveScreenshot(`board-move-to.${theme}.png`, { animations: 'disabled' });
  });
}
