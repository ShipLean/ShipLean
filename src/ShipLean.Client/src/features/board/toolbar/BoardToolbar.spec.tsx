import { expect, test } from '@playwright/experimental-ct-react';
import { BoardHarness } from '../ct/BoardHarness';
import { LocationProbe } from '../ct/LocationProbe';
import { storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

test('Toolbar_WhenBoardSettingsClicked_ShouldAskForTheColumnsPane', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(
    <>
      <BoardHarness />
      <LocationProbe />
    </>,
    { hooksConfig: { route: '/p/SHIP/boards/stories' } },
  );

  await page.getByTestId(ids.settingsOpen).click();

  await expect(page.getByTestId(ids.locationProbe)).toHaveText(
    '/p/SHIP/boards/stories?settings=columns',
  );
});
