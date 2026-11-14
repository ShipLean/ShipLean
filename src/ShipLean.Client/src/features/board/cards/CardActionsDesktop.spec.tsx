import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { BoardHarness } from '../ct/BoardHarness';
import { storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

test.use({ viewport: { width: 1280, height: 800 } });

test('Card_WhenDesktop_ShouldNotRenderMore', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, { hooksConfig: { route: '/p/SHIP/boards/stories' } });

  await expect(page.getByTestId(ids.card).first()).toBeVisible();
  await expect(page.getByTestId(touchTestIds.moreButton)).toHaveCount(0);
});
