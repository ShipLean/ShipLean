import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../../components/testIds';
import { touchTestIds } from '../../../../touch/testIds';
import { actionDialog, stubRetros } from '../../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../../testIds';
import { RetroActionDialogHarness } from './RetroActionDialogHarness';

test('RetroActionDialog_WhenOnPhone_ShouldPutCreateTopRightAndNotFocusTitle', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await page.getByRole('button', { name: 'open' }).tap();
  const dialog = page.getByTestId(ids.retroActionDialog);
  const title = page.getByTestId(ids.retroActionTitle);
  await expect(title).toHaveValue(actionDialog.selected[0].text.slice(0, 80));
  await expect(title).not.toBeFocused();
  await expect(dialog).toBeFocused();

  // Full screen, ✕ top left, Create top right, the toggle under them, the note at the end, no footer.
  expect(await dialog.boundingBox()).toEqual({ x: 0, y: 0, width: 390, height: 844 });
  const close = (await page.getByTestId(touchTestIds.dialogClose).boundingBox())!;
  const create = page.getByTestId(touchTestIds.dialogPrimary).getByTestId(ids.retroActionCreate);
  await expect(create).toHaveText('Create');
  const createBox = (await create.boundingBox())!;
  expect(close.x).toBeLessThan(16);
  expect(createBox.x + createBox.width).toBeGreaterThan(390 - 24);
  expect(createBox.height).toBeGreaterThanOrEqual(32);
  const toggle = (await page.getByTestId(ids.retroActionModeNew).boundingBox())!;
  expect(toggle.y).toBeGreaterThan(createBox.y + createBox.height);
  // The toggle keeps its height in the scrolling body: the Title field starts below it.
  const titleBox = (await title.boundingBox())!;
  expect(toggle.y + toggle.height).toBeLessThanOrEqual(titleBox.y);
  const lastPick = (await page.getByTestId(ids.retroActionPick).last().boundingBox())!;
  expect((await page.getByTestId(ids.retroActionStartsNote).boundingBox())!.y).toBeGreaterThan(
    lastPick.y,
  );
  await expect(page.getByTestId(ids.retroActionSave)).toHaveCount(0);
  await expect(page.getByTestId(componentTestIds.dialogClose)).toHaveCount(0);

  // Add to existing: the top right reads Add, and the note goes.
  await page.getByTestId(ids.retroActionModeExisting).tap();
  await expect(create).toHaveText('Add');
  await expect(page.getByTestId(ids.retroActionStartsNote)).toHaveCount(0);

  // It behaves as the desktop dialog: Create posts the new action item and closes.
  await page.getByTestId(ids.retroActionModeNew).tap();
  await create.tap();
  await expect
    .poll(() => stub.writes().map((w) => `${w.method} ${w.path}`))
    .toEqual(['POST /api/projects/p1/retros/actions']);
  await expect(dialog).toHaveCount(0);
});

for (const theme of ['light', 'dark'] as const) {
  test(`RetroActionDialog_WhenOnPhone_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<RetroActionDialogHarness />);
    await page.getByRole('button', { name: 'open' }).tap();
    await expect(page.getByTestId(ids.retroActionCreate)).toBeVisible();
    await expect(page).toHaveScreenshot(`retro-action-dialog-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
