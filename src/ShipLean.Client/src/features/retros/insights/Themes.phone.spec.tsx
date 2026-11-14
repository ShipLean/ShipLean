import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroInsightsDto } from '@shiplean/core/api/retros';
import { touchTestIds } from '../../../touch/testIds';
import { insightsWeeks, retroInsights, retroSearch, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { ThemesAddressHarness } from './ct/ThemesAddressHarness';

// Recurring themes on a phone (SHIP-731 D1, D4, D9): a card per theme with its heat strip, the theme sheet and Rename…
// in it. Regenerate the baselines with scripts/update-page-shots.sh src/features/retros/insights/Themes.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const open = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/insights${query}` } });
// No Unthemed row: at touch it is a card of L731d's (UnthemedRow renders a table row until then, which this layout can't hold).
const withoutUnthemed = (insights: RetroInsightsDto): RetroInsightsDto => ({
  ...insights,
  unthemed: { counts: insights.unthemed.counts.map(() => 0), noSprint: 0, total: 0 },
});
const base = withoutUnthemed(retroInsights);
const themeRows = retroInsights.themes;
const helped = themeRows.find((row) => row.lastVerdict === 'helped')!;
const recurring = themeRows.find((row) => row.status === 'recurring' && row.lastVerdict === null)!;
const sheetQuery = (id: string) => `?sheet=theme:${id}`;
const address = (page: Parameters<typeof stubRetros>[0]) => page.getByTestId('test-address');

// Twelve weeks, every theme's counts stretched to match.
const twelveWeeks: RetroInsightsDto = withoutUnthemed({
  ...insightsWeeks,
  range: 'w12',
  columns: Array.from({ length: 12 }, (_, i) => ({
    key: `2026-0${i < 4 ? 8 : 9}-${String(1 + i * 2).padStart(2, '0')}`,
    label: `${1 + i * 2} ${i < 4 ? 'Aug' : 'Sep'}`,
    kind: 'week' as const,
    isCurrent: i === 11,
  })),
  themes: insightsWeeks.themes.map((row) => ({ ...row, counts: [...row.counts, ...row.counts] })),
});

for (const colour of THEMES) {
  test(`ThemeCards_WhenOpened_ShouldMatchItsScreenshot_retro-insights-phone_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    const screen = await mount(<ThemesAddressHarness insights={base} />, open());
    await expect(page.getByTestId(ids.retroThemeCard)).toHaveCount(themeRows.length);
    await expect(screen).toHaveScreenshot(`retro-insights-phone.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`ThemeCards_WhenTwelveWeeksShow_ShouldMatchItsScreenshot_retro-insights-weeks-phone_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    const screen = await mount(<ThemesAddressHarness insights={twelveWeeks} />, open('?range=w12'));
    await expect(page.getByTestId(ids.retroThemeCard)).toHaveCount(themeRows.length);
    await expect(screen).toHaveScreenshot(`retro-insights-weeks-phone.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`ThemeSheet_WhenOpened_ShouldMatchItsScreenshot_retro-insights-theme_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(page.getByTestId(ids.retroThemeSheetMention).first()).toBeVisible();
    await expect(sheet).toHaveScreenshot(`retro-insights-theme.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`ThemeSheet_WhenRenaming_ShouldMatchItsScreenshot_retro-insights-rename_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
    await page.getByTestId(ids.retroThemeSheetRename).tap();
    await page.getByTestId(ids.retroThemeRenameField).fill('Handoff contracts missing');
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toHaveScreenshot(`retro-insights-rename.${colour}.png`, {
      animations: 'disabled',
    });
  });
}

test('ThemeCards_WhenOpenedOnAPhone_ShouldShowAStripCellPerColumnAndTheAxis', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={twelveWeeks} />, open('?range=w12'));
  const cards = page.getByTestId(ids.retroThemeCard);
  await expect(cards).toHaveCount(twelveWeeks.themes.length);
  for (const card of await cards.all()) {
    await expect(card.getByTestId(ids.retroHeatCell)).toHaveCount(12);
  }
  const first = cards.first();
  const row = twelveWeeks.themes[0];
  await expect(first.getByTestId(ids.retroHeatTotal)).toHaveText(String(row.total));
  await expect(page.getByTestId(ids.retroThemeAxis)).toContainText('1 Aug');
  await expect(page.getByTestId(ids.retroThemeAxis)).toContainText('23 Sep');
  await expect(page.getByTestId(ids.retroRecurringTable)).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
  // Weeks have no "no sprint" count; sprints do.
  await expect(first.getByTestId(ids.retroHeatNoSprint)).toHaveCount(0);
});

test('ThemeCards_WhenSprintsShow_ShouldSayHowManyHaveNoSprint', async ({ mount, page }) => {
  await stubRetros(page);
  const withLoose = {
    ...base,
    themes: base.themes.map((row, i) => (i === 0 ? { ...row, noSprint: 4 } : row)),
  };
  await mount(<ThemesAddressHarness insights={withLoose} />, open());
  await expect(
    page.getByTestId(ids.retroThemeCard).first().getByTestId(ids.retroHeatNoSprint),
  ).toHaveText('4 no sprint');
});

test('ThemeCards_WhenATabletShows_ShouldPutTwoInARow', async ({ mount, page }) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open());
  const cards = page.getByTestId(ids.retroThemeCard);
  const [a, b, c] = await Promise.all([0, 1, 2].map((i) => cards.nth(i).boundingBox()));
  expect(Math.abs(a!.y - b!.y)).toBeLessThan(2);
  expect(c!.y).toBeGreaterThan(a!.y + 20);
});

test('ThemeCards_WhenAboveTheTouchWidth_ShouldKeepTheTable', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await expect(page.getByTestId(ids.retroRecurringTable)).toBeVisible();
  await expect(page.getByTestId(ids.retroThemeCard)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('ThemeCards_WhenACardIsTapped_ShouldWriteTheSheetToTheAddressAndCloseRemovesIt', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open('?range=6'));
  const card = page
    .getByTestId(ids.retroThemeCard)
    .and(page.locator(`[data-theme-id="${helped.theme.id}"]`));
  await card.getByTestId(ids.retroHeatStrip).tap();
  await expect(address(page)).toHaveText(`?range=6&sheet=theme%3A${helped.theme.id}`);
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText(helped.theme.name);
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(sheet).toHaveCount(0);
  await expect(address(page)).toHaveText('?range=6');
  // The name opens it too.
  await card.getByTestId(ids.retroThemeCardOpen).tap();
  await expect(sheet).toBeVisible();
});

test('ThemeCards_WhenAnActionChipIsTapped_ShouldOpenTheItemNotTheSheet', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open());
  const chip = page.getByTestId(ids.retroThemeCard).first().locator('a').first();
  expect((await chip.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  await chip.tap();
  await expect(address(page)).toContainText('item=');
  await expect(address(page)).not.toContainText('sheet=');
});

test('ThemeSheet_WhenOpened_ShouldShowEverythingAboutTheTheme', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  const body = page.getByTestId(ids.retroThemeSheet);
  await expect(body.getByTestId(ids.retroThemeSheetStatus)).toContainText('Resolved');
  await expect(body.getByTestId(ids.retroThemeSheetStatus)).toContainText(
    'a closed action item on this theme has the verdict Helped',
  );
  const counts = body.getByTestId(ids.retroThemeSheetCount);
  await expect(counts).toHaveCount(retroInsights.columns.length);
  for (const [i, column] of retroInsights.columns.entries()) {
    await expect(counts.nth(i)).toHaveAttribute('data-column-key', column.key);
    await expect(counts.nth(i)).toHaveAttribute('data-count', String(helped.counts[i]));
  }
  await expect(body.getByTestId(ids.retroThemeSheetNoSprint)).toContainText('No sprint 0');
  await expect(body.getByTestId(ids.retroThemeSheetTotal)).toContainText(`Total ${helped.total}`);
  const actions = body.getByTestId(ids.retroThemeSheetAction);
  await expect(actions).toHaveCount(helped.actionItems.length);
  await expect(actions.first()).toContainText(helped.actionItems[0].title);
  await expect(body.getByTestId(ids.retroThemeSheetAddAction)).toContainText('+ Action item on');
  const change = body.getByTestId(ids.retroThemeSheetLastChange);
  await expect(change).toContainText('Helped');
  await expect(change).toContainText('at most half of what they were before');
  await expect(change).toContainText('3 before, 0.5 after');
  const mentions = body.getByTestId(ids.retroThemeSheetMention);
  await expect(mentions).toHaveCount(Math.min(3, retroSearch.rows.length));
  await expect(body.getByTestId(ids.retroThemeSheetAllInSearch)).toHaveText(
    `All ${retroSearch.total} in Search`,
  );
  await expect(body.getByTestId(ids.retroThemeSheetAllInSearch)).toHaveAttribute(
    'href',
    new RegExp(`/p/SHIP/retros\\?theme=${helped.theme.id}$`),
  );
});

test('ThemeSheet_WhenNoChangeShipped_ShouldSayNoChangeShipped', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(recurring.theme.id)));
  const change = page.getByTestId(ids.retroThemeSheetLastChange);
  await expect(change).toContainText('No change has shipped for this theme.');
  await expect(page.getByTestId(ids.retroThemeSheetSeeImpact)).toHaveCount(0);
});

test('ThemeSheet_WhenSeeItOnImpactIsTapped_ShouldWriteTheImpactViewAndCloseTheSheet', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await page.getByTestId(ids.retroThemeSheetSeeImpact).tap();
  await expect(address(page)).toHaveText('?view=impact');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('ThemeSheet_WhenMergeIntoIsTapped_ShouldWriteMergeWithTheTheme', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await page.getByTestId(ids.retroThemeSheetMerge).tap();
  await expect(address(page)).toHaveText(`?merge=${helped.theme.id}`);
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('ThemeSheet_WhenAddActionIsTapped_ShouldOpenTheDialogOnTheThemeWithTheSheetClosed', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await page.getByTestId(ids.retroThemeSheetAddAction).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(address(page)).toHaveText('');
});

test('ThemeSheet_WhenAnItemIsOpenedFromIt_ShouldGiveWayToIt', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  const sheet = page.getByTestId(touchTestIds.sheet);
  await sheet.locator('a').first().tap();
  await expect(address(page)).toContainText('item=');
  await expect(sheet).toHaveCount(0);
});

test('Rename_WhenSavedInTheSheet_ShouldPutTheNewNameAndLeaveTheForm', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await page.getByTestId(ids.retroThemeSheetRename).tap();
  const field = page.getByTestId(ids.retroThemeRenameField);
  expect(await field.evaluate((el) => getComputedStyle(el).fontSize)).toBe('16px');
  await expect(field).toHaveValue(helped.theme.name);
  await expect(page.getByTestId(ids.retroThemeRenameSave)).toBeDisabled();
  await field.fill('Handoff contracts missing');
  await expect(page.getByTestId(touchTestIds.sheetFooter)).toContainText('Discard');
  await page.getByTestId(ids.retroThemeRenameSave).tap();
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].method).toBe('PUT');
  expect(stub.writes()[0].path).toBe(`/api/retros/themes/${helped.theme.id}`);
  expect(stub.writes()[0].body).toEqual({ name: 'Handoff contracts missing' });
  await expect(field).toHaveCount(0);
  await expect(page.getByTestId(ids.retroThemeSheetRename)).toBeVisible();
});

test('Rename_WhenDiscarded_ShouldKeepTheNameAndWriteNothing', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await page.getByTestId(ids.retroThemeSheetRename).tap();
  await page.getByTestId(ids.retroThemeRenameField).fill('Something else');
  await page.getByTestId(ids.retroThemeRenameDiscard).tap();
  await expect(page.getByTestId(ids.retroThemeRenameField)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText(helped.theme.name);
  expect(stub.writes()).toHaveLength(0);
});

test('Rename_WhenTheNameIsTaken_ShouldSayItUnderTheField', async ({ mount, page }) => {
  await stubRetros(page);
  await page.route(
    (url) => /^\/api\/retros\/themes\/[^/]+$/.test(url.pathname),
    (route) =>
      route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'name taken', code: 'conflict', field: 'name' }),
      }),
  );
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await page.getByTestId(ids.retroThemeSheetRename).tap();
  await page.getByTestId(ids.retroThemeRenameField).fill('Brief too vague');
  await page.getByTestId(ids.retroThemeRenameSave).tap();
  await expect(page.getByTestId(ids.retroThemeRenameError)).toHaveText('That name is taken');
});
