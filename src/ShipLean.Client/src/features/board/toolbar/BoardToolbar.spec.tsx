import { expect, test } from '@playwright/experimental-ct-react';
import { BoardHarness } from '../ct/BoardHarness';
import { LocationProbe } from '../ct/LocationProbe';
import { storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

test('Toolbar_WhenOnStories_ShouldLinkTheThreeBoardsAndMarkTheCurrentOne', async ({
  mount,
  page,
}) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, { hooksConfig: { route: '/p/SHIP/boards/stories' } });

  const options = page.getByTestId(ids.levelOption);
  await expect(options).toHaveText(['Epics', 'Features', 'Stories']);
  await expect(options.nth(0)).toHaveAttribute('href', '/p/SHIP/boards/epics');
  await expect(options.nth(1)).toHaveAttribute('href', '/p/SHIP/boards/features');
  await expect(options.nth(2)).toHaveAttribute('href', '/p/SHIP/boards/stories');
  await expect(options.nth(2)).toHaveAttribute('aria-current', 'page');
  await expect(options.nth(0)).not.toHaveAttribute('aria-current', 'page');
});

test('Toolbar_WhenLevelClicked_ShouldGoToThatBoard', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(
    <>
      <BoardHarness />
      <LocationProbe />
    </>,
    { hooksConfig: { route: '/p/SHIP/boards/stories' } },
  );

  await page.getByTestId(ids.levelOption).filter({ hasText: 'Features' }).click();

  await expect(page.getByTestId(ids.locationProbe)).toHaveText('/p/SHIP/boards/features');
});

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
