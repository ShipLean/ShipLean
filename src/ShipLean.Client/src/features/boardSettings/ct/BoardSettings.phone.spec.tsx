import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { settingsDialogTestIds as ids } from '../../settingsDialog/testIds';
import { BoardSettingsHarness } from './BoardSettingsHarness';
import { BOARD_ID, boardSettingsDto, PROJECT_ID } from './boardSettingsFixtures';
import { boardHarnessIds } from './harnessIds';
import { stubBoard } from './stubBoard';

// The shared settings frame on a phone (SHIP-725), through the Board settings harness: section chips and the K5 SaveBar.

test('BoardSettings_WhenOpenedOnPhone_ShouldShowSectionChipsAndTheSaveBar', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, boardSettingsDto());
  await mount(<BoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} apply="onSave" />);

  const annotations = page.getByTestId(`${ids.navItem}-annotations`);
  const swimlanes = page.getByTestId(`${ids.navItem}-swimlanes`);
  const first = await annotations.boundingBox();
  const second = await swimlanes.boundingBox();
  expect(first?.y, 'the chips are one row').toBe(second?.y);
  expect(first?.height).toBeGreaterThanOrEqual(32);
  await expect(annotations).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);

  await page.getByTestId(boardHarnessIds.toggleTasks).tap();
  const bar = page.getByTestId(touchTestIds.saveBar);
  await expect(bar.getByTestId(ids.footerDirty)).toHaveText('Unsaved: Annotations');
  await expect(page.getByTestId(`${ids.navDot}-annotations`)).toBeVisible();

  await bar.getByTestId(touchTestIds.saveBarSave).tap();
  await expect(page.getByTestId(boardHarnessIds.closed)).toBeAttached();
  expect(puts).toHaveLength(1);
  expect(puts[0]?.settings.annotations.tasks).toBe(false);
});
