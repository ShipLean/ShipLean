import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroThemeOptionDto } from '@shiplean/core/api/retros';
import { retroSharedTestIds as ids } from './testIds';
import { ThemePicker } from './ThemePicker';

const themes: RetroThemeOptionDto[] = [
  { theme: { id: 't-shots', name: 'Screenshot baselines need CI', tone: 'bad' }, pointCount: 3 },
  { theme: { id: 't-merge', name: 'Merge conflicts', tone: 'bad' }, pointCount: 2 },
];

test('ThemePicker_WhenExistingThemePicked_ShouldCallOnPickWithItsId', async ({ mount, page }) => {
  const picks: unknown[] = [];
  await mount(
    <ThemePicker themes={themes} mode="pick" value={null} onPick={(p) => picks.push(p)} />,
  );
  await page.getByRole('option', { name: /Merge conflicts/ }).click();
  expect(picks).toEqual(['t-merge']);
});

test('ThemePicker_WhenNewNameAdded_ShouldCallOnPickWithName', async ({ mount, page }) => {
  const picks: unknown[] = [];
  await mount(
    <ThemePicker themes={themes} mode="pick" value={null} onPick={(p) => picks.push(p)} />,
  );
  await page.getByRole('combobox').fill('Flaky env');
  await page.getByTestId(ids.retroThemePickerAdd).click();
  expect(picks).toEqual([{ name: 'Flaky env' }]);
});

test('ThemePicker_WhenTypedNameDiffersOnlyInCase_ShouldMatchAndOfferNoAdd', async ({
  mount,
  page,
}) => {
  const picks: unknown[] = [];
  await mount(
    <ThemePicker themes={themes} mode="auto" value={null} onPick={(p) => picks.push(p)} />,
  );
  await page.getByRole('combobox').fill('MERGE CONFLICTS');
  await expect(page.getByRole('option')).toHaveCount(1);
  await expect(page.getByTestId(ids.retroThemePickerAdd)).toHaveCount(0);
  await page.getByRole('combobox').press('Enter');
  expect(picks).toEqual(['t-merge']);
});

test('ThemePicker_WhenUsingKeyboard_ShouldMoveWithArrowsAndPickWithEnter', async ({
  mount,
  page,
}) => {
  const picks: unknown[] = [];
  await mount(
    <ThemePicker themes={themes} mode="pick" value={null} onPick={(p) => picks.push(p)} />,
  );
  const input = page.getByRole('combobox');
  await input.focus();
  await input.press('ArrowDown');
  await input.press('Enter');
  expect(picks).toEqual(['t-merge']);
});

test('ThemePicker_WhenTypingNewNameWithKeyboard_ShouldPickItWithEnter', async ({ mount, page }) => {
  const picks: unknown[] = [];
  await mount(
    <ThemePicker themes={themes} mode="pick" value={null} onPick={(p) => picks.push(p)} />,
  );
  const input = page.getByRole('combobox');
  await input.fill('Brand new');
  await input.press('Enter');
  expect(picks).toEqual([{ name: 'Brand new' }]);
});
