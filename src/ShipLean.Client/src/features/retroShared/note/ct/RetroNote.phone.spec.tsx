import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { touchTestIds } from '../../../../touch/testIds';
import { noteStates, retroThemes, sprintRetro, stubRetros } from '../../../retros/ct/retroFixtures';
import type { AddTarget } from '../../board/AddTarget';
import { RetroBoardHarness } from '../../board/ct/RetroBoardHarness';
import { retroSharedTestIds as ids } from '../../testIds';
import { RetroNoteHarness } from './RetroNoteHarness';

// Your own undecided Went badly point with a root cause: ⋯, # theme, both buttons and the cause all show.
const own: RetroPointDto = {
  ...noteStates.undecided,
  category: 'wentBadly',
  canArchive: true,
  rootCause: 'The review checklist lives in a wiki nobody opens.',
};

const fontSize = (locator: Locator) => locator.evaluate((el) => getComputedStyle(el).fontSize);

test('RetroNote_WhenMoreTappedOnPhone_ShouldOfferMoveToThemeAndArchive', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<RetroNoteHarness point={own} />);
  const more = page.getByTestId(ids.retroNote).getByTestId(touchTestIds.moreButton);
  const box = (await more.boundingBox())!;
  expect(box.width).toBeGreaterThanOrEqual(32);
  expect(box.height).toBeGreaterThanOrEqual(32);
  for (const id of [ids.retroNoteAddAction, ids.retroNoteNotNeeded]) {
    expect((await page.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }

  await more.tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet).toBeVisible();
  const rows = sheet.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveText(['Move to theme', 'Archive']);
  // No desktop popover at touch.
  await expect(page.getByTestId(ids.retroNoteArchive)).toHaveCount(0);

  // Move to theme closes the action sheet and opens the theme sheet.
  await rows.filter({ hasText: 'Move to theme' }).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(1);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Move to theme');
  await expect(page.getByTestId(ids.retroThemePicker)).toBeVisible();
  await expect(page.getByTestId(ids.retroThemeSheetNew)).toBeDisabled();
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);

  // Archive posts the archive and closes the sheet.
  await more.tap();
  await page.getByTestId(touchTestIds.actionSheetRow).filter({ hasText: 'Archive' }).tap();
  await expect
    .poll(() => stub.writes().map((w) => `${w.method} ${w.path}`))
    .toEqual([`POST /api/retros/points/${own.id}/archive`]);
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('RetroNote_WhenSomeoneElsesPointOnPhone_ShouldShowNoMore', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={{ ...own, canArchive: false }} />);
  await expect(page.getByTestId(ids.retroNoteTheme)).toBeEnabled();
  await expect(page.getByTestId(touchTestIds.moreButton)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroNoteMore)).toHaveCount(0);
});

test('RetroNote_WhenThemeTappedOnPhone_ShouldOpenThemeSheetWithoutFocusingSearch', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<RetroNoteHarness point={own} />);
  const theme = page.getByTestId(ids.retroNoteTheme);
  await expect(theme).toBeEnabled();
  expect((await theme.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  await theme.tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Move to theme');
  const picker = sheet.getByTestId(ids.retroThemePicker);
  await expect(picker).toBeVisible();
  const search = picker.getByRole('combobox');
  await expect(search).not.toBeFocused();
  await expect(sheet).toBeFocused();
  expect(await fontSize(search)).toBe('16px');

  const target = retroThemes.themes.find((t) => t.theme.id !== own.theme?.id)!;
  await picker.getByRole('option', { name: target.theme.name }).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0]).toMatchObject({
    method: 'PUT',
    path: `/api/retros/points/${own.id}/theme`,
    body: { themeId: target.theme.id, themeName: null },
  });
});

test('RetroNote_WhenNotNeededTappedOnPhone_ShouldUse16pxReasonAnd32pxButtons', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={own} />);
  await page.getByTestId(ids.retroNoteNotNeeded).tap();
  expect(await fontSize(page.getByTestId(ids.retroNoteWhyText))).toBe('16px');
  for (const id of [ids.retroNoteWhySave, ids.retroNoteWhyCancel]) {
    expect((await page.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
});

const sprintAdd: AddTarget = {
  kind: 'sprint',
  sprintId: 'sprint-24',
  about: [
    { value: 'sprint-24', label: 'Sprint' },
    {
      value: sprintRetro.coverage[0].item.id,
      label: `${sprintRetro.coverage[0].item.key} ${sprintRetro.coverage[0].item.title}`,
    },
  ],
};

test('AddNote_WhenOnPhone_ShouldUse16pxTextAndNativeAboutSelect', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness add={sprintAdd} />);
  const column = page.locator(`[data-testid="${ids.retroBoardColumn}"][data-category="wentBadly"]`);
  const add = column.getByTestId(ids.retroBoardAdd);
  expect((await add.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  await add.tap();

  for (const id of [ids.retroBoardAddText, ids.retroBoardAddCause, ids.retroBoardAddAbout]) {
    expect(await fontSize(page.getByTestId(id))).toBe('16px');
  }
  const about = page.getByTestId(ids.retroBoardAddAbout);
  expect(await about.evaluate((el) => el.tagName)).toBe('SELECT');
  await about.selectOption(sprintRetro.coverage[0].item.id);
  for (const id of [ids.retroBoardAddSave, ids.retroBoardAddCancel]) {
    expect((await page.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
});

for (const theme of ['light', 'dark'] as const) {
  test(`RetroNote_WhenOnPhone_ShouldMatchItsScreenshots_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<RetroNoteHarness point={own} />);
    const note = page.getByTestId(ids.retroNote);
    await expect(page.getByTestId(ids.retroNoteTheme)).toBeEnabled();
    await expect(note).toHaveScreenshot(`retro-note-phone.${theme}.png`, {
      animations: 'disabled',
    });

    await page.getByTestId(ids.retroNoteTheme).tap();
    await expect(page.getByTestId(ids.retroThemePicker)).toBeVisible();
    await expect(page).toHaveScreenshot(`retro-theme-sheet-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
