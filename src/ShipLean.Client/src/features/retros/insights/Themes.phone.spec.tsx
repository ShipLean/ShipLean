import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { RetroInsightsDto } from '@shiplean/core/api/retros';
import { touchTestIds } from '../../../touch/testIds';
import {
  actionDialog,
  insightsWeeks,
  retroInsights,
  retroSearch,
  stubRetros,
} from '../ct/retroFixtures';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { InsightsAddressHarness } from '../ct/InsightsAddressHarness';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { ThemesAddressHarness } from './ct/ThemesAddressHarness';

// Recurring themes on a phone (SHIP-731 D1, D4, D9): the heading with What these mean, a card per theme with its heat
// strip, the theme sheet and Rename… in it, and the Unthemed card with its sheet (RETRO-11, 12, 13). Regenerate the baselines with scripts/update-page-shots.sh src/features/retros/insights/Themes.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const open = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/insights${query}` } });
// No Unthemed card in the theme shots, so they show the themes alone; the Unthemed tests below arrange their own.
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
  // A no-sprint count on every theme, so the cards' missing "n no sprint" in week mode is the mode's doing, not a 0.
  themes: insightsWeeks.themes.map((row) => ({
    ...row,
    counts: [...row.counts, ...row.counts],
    noSprint: 2,
  })),
});
// The design's Unthemed (insights-unthemed): mentions in three of six sprints, 6 on retros with no sprint, 12 in all.
const unthemedCounts = [0, 0, 1, 0, 2, 3];
const withUnthemed: RetroInsightsDto = {
  ...retroInsights,
  unthemed: { counts: unthemedCounts, noSprint: 6, total: 12 },
};
const unthemedQuery = '?sheet=theme:unthemed';

/**
 * Holds what a theme sheet loads (its mentions from Search, its open points) until the returned function is called. A
 * sheet that grows when they arrive is
 * bottom-anchored with a fractional height, so it moves by a fraction of a pixel, and the lines it had already painted
 * keep their old pixel rounding (some land a pixel lower). An answer that raced the sheet's first paint gave one shot or
 * the other at random (SHIP-2110); answering after the paint gives the same one every run.
 */
async function holdWhatTheSheetLoads(page: Page): Promise<() => void> {
  let answer!: () => void;
  const held = new Promise<void>((resolve) => (answer = resolve));
  await page.route(
    (url) => /^\/api\/projects\/[^/]+\/retros(\/action-dialog)?$/.test(url.pathname),
    async (route) => {
      await held;
      await route.fallback();
    },
  );
  return answer;
}

/** Resolves once the browser has painted the current page: a frame callback runs before a paint, its next one after it. */
const afterTheNextPaint = (page: Page) =>
  page.evaluate(
    () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))),
  );

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
    // 85dvh of 840 is a whole number of pixels, so the sheet's text lands on the same pixels every run.
    await page.setViewportSize({ width: 390, height: 840 });
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    // What the sheet loads always arrives after it has painted, as on a real network (see holdWhatTheSheetLoads).
    const answer = await holdWhatTheSheetLoads(page);
    await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.retroThemeSheet)).toBeVisible();
    await afterTheNextPaint(page);
    answer();
    // Everything the sheet loads is in before the shot: the bottom-anchored sheet grows as it arrives.
    await expect(page.getByTestId(ids.retroThemeSheetMention)).toHaveCount(3);
    await expect(page.getByTestId(ids.retroThemeSheetAddAction)).toContainText('open point');
    await expect(sheet).toHaveScreenshot(`retro-insights-theme.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`ThemeSheet_WhenAddActionIsTapped_ShouldMatchItsScreenshot_retro-insights-action-phone_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
    await page.getByTestId(ids.retroThemeSheetAddAction).tap();
    await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
    await expect(page.getByTestId(shared.retroActionCreate)).toBeVisible();
    await expect(page).toHaveScreenshot(`retro-insights-action-phone.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`ThemeSheet_WhenRenaming_ShouldMatchItsScreenshot_retro-insights-rename_${colour}`, async ({
    mount,
    page,
  }) => {
    // 85dvh of 840 is a whole number of pixels, so the sheet's text lands on the same pixels every run.
    await page.setViewportSize({ width: 390, height: 840 });
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    // What the sheet loads always arrives after it has painted, as on a real network (see holdWhatTheSheetLoads).
    const answer = await holdWhatTheSheetLoads(page);
    await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
    await expect(page.getByTestId(ids.retroThemeSheet)).toBeVisible();
    await afterTheNextPaint(page);
    answer();
    await expect(page.getByTestId(ids.retroThemeSheetMention)).toHaveCount(3);
    await expect(page.getByTestId(ids.retroThemeSheetAddAction)).toContainText('open point');
    await page.getByTestId(ids.retroThemeSheetRename).tap();
    await page.getByTestId(ids.retroThemeRenameField).fill('Handoff contracts missing');
    await expect(page.getByTestId(ids.retroThemeRenameField)).toBeFocused();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toHaveScreenshot(`retro-insights-rename.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`UnthemedSheet_WhenOpened_ShouldMatchItsScreenshot_retro-insights-unthemed_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width: 390, height: 840 });
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    // What the sheet loads always arrives after it has painted, as on a real network (see holdWhatTheSheetLoads).
    const answer = await holdWhatTheSheetLoads(page);
    await mount(<ThemesAddressHarness insights={withUnthemed} />, open(unthemedQuery));
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.retroUnthemedSheet)).toBeVisible();
    await afterTheNextPaint(page);
    answer();
    // Everything the sheet loads is in before the shot: the bottom-anchored sheet grows as it arrives.
    await expect(sheet.getByTestId(ids.retroThemeSheetMention)).toHaveCount(3);
    await expect(sheet.getByTestId(ids.retroThemeSheetAllInSearch)).toHaveText(
      `All ${retroSearch.total} in Search`,
    );
    await expect(sheet).toHaveScreenshot(`retro-insights-unthemed.${colour}.png`, {
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
  // The dialog's own count of the theme's undecided points, not the longer list it offers beyond them.
  expect(actionDialog.selected.length).not.toBe(actionDialog.offered.length);
  await expect(body.getByTestId(ids.retroThemeSheetAddAction)).toHaveText(
    `+ Action item on ${actionDialog.selected.length} open point`,
  );
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

test('InsightsUnthemedCard_WhenTapped_ShouldOpenTheUnthemedSheet', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<ThemesAddressHarness insights={withUnthemed} />, open('?range=6'));
  const card = page.getByTestId(ids.retroInsightsUnthemed);
  await card.getByTestId(ids.retroHeatStrip).tap();
  await expect(address(page)).toHaveText('?range=6&sheet=theme%3Aunthemed');
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Unthemed');
  const body = sheet.getByTestId(ids.retroUnthemedSheet);
  await expect(body).toContainText('Points with no theme');
  const counts = body.getByTestId(ids.retroThemeSheetCount);
  await expect(counts).toHaveCount(retroInsights.columns.length);
  for (const [i, column] of retroInsights.columns.entries()) {
    await expect(counts.nth(i)).toHaveAttribute('data-column-key', column.key);
    await expect(counts.nth(i)).toHaveAttribute('data-count', String(unthemedCounts[i]));
  }
  await expect(body.getByTestId(ids.retroThemeSheetNoSprint)).toContainText('No sprint 6');
  await expect(body.getByTestId(ids.retroThemeSheetTotal)).toContainText('Total 12');
  await expect(body.getByTestId(ids.retroUnthemedSheetGiveThemes)).toHaveAttribute(
    'href',
    /\/p\/SHIP\/retros\?unthemed=true$/,
  );
  // Its latest mentions are the unthemed points, from Search.
  await expect(body.getByTestId(ids.retroThemeSheetMention)).toHaveCount(3);
  expect(stub.reads(`/projects/${insightsProject.id}/retros`).map((c) => c.query)).toContainEqual(
    expect.stringContaining('unthemed=true'),
  );
  await expect(body.getByTestId(ids.retroThemeSheetAllInSearch)).toHaveAttribute(
    'href',
    /\/p\/SHIP\/retros\?unthemed=true$/,
  );
  // Unthemed is never judged: no status, action items or last change, and no Rename or Merge.
  await expect(body.getByTestId(ids.retroThemeSheetStatus)).toHaveCount(0);
  await expect(body.getByTestId(ids.retroThemeSheetAddAction)).toHaveCount(0);
  await expect(body.getByTestId(ids.retroThemeSheetRename)).toHaveCount(0);
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(sheet).toHaveCount(0);
  await expect(address(page)).toHaveText('?range=6');
  // Its name opens it too.
  await card.getByTestId(ids.retroThemeCardOpen).tap();
  await expect(page.getByTestId(ids.retroUnthemedSheet)).toBeVisible();
});

test('InsightsUnthemedCard_WhenGiveThemThemesIsTapped_ShouldGoToSearchNotTheSheet', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={withUnthemed} />, open('?range=6'));
  await page.getByTestId(ids.retroInsightsUnthemed).getByTestId(ids.retroUnthemedLink).tap();
  await expect(address(page)).toHaveText('?unthemed=true');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('InsightsHeading_ShouldOpenWhatTheseMean', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<InsightsAddressHarness project={insightsProject} />, open('?range=6'));
  const summary = page.getByTestId(ids.retroThemesHeadingSummary);
  const recurringCount = retroInsights.themes.filter((row) => row.status === 'recurring').length;
  await expect(summary).toHaveText(`By theme, Sprint 19 – 24. ${recurringCount} recurring.`);
  const link = page.getByTestId(ids.retroThemesHeadingDefs);
  // On the heading's line above the summary, at its right end, and a touch target.
  const [linkBox, summaryBox] = await Promise.all([link.boundingBox(), summary.boundingBox()]);
  expect(linkBox!.y + linkBox!.height).toBeLessThanOrEqual(summaryBox!.y + 1);
  expect(linkBox!.x).toBeGreaterThan(summaryBox!.x + summaryBox!.width / 2);
  expect(linkBox!.x + linkBox!.width).toBeGreaterThan(summaryBox!.x + summaryBox!.width - 2);
  expect(linkBox!.height).toBeGreaterThanOrEqual(32);
  await link.tap();
  await expect(address(page)).toHaveText('?range=6&sheet=defs');
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('What these mean');
});

test('InsightsHeading_WhenAboveTheTouchWidth_ShouldKeepTheDesktopHeading', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open());
  await expect(page.getByTestId(ids.retroRecurringTable)).toBeVisible();
  await expect(page.getByTestId(ids.retroThemesHeadingDefs)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroThemesHeadingSummary)).toHaveCount(0);
});

test('ThemeCards_WhenAThemeHasActionItems_ShouldShowThemWithoutAddAction', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const bare = {
    ...base,
    themes: base.themes.map((row, i) => (i === 1 ? { ...row, actionItems: [] } : row)),
  };
  await mount(<ThemesAddressHarness insights={bare} />, open());
  const cards = page.getByTestId(ids.retroThemeCard);
  await expect(cards).toHaveCount(base.themes.length);
  await expect(cards.nth(0).locator('a')).toHaveCount(base.themes[0].actionItems.length);
  await expect(cards.nth(0).getByTestId(ids.retroInsightsThemeAction)).toHaveCount(0);
  await expect(cards.nth(1).getByTestId(ids.retroInsightsThemeAction)).toHaveCount(1);
  await expect(page.getByTestId(ids.retroInsightsThemeAction)).toHaveCount(1);
});

test('ThemeCards_WhenAChangeShipped_ShouldLabelItsVerdictLastChange', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open());
  const card = page
    .getByTestId(ids.retroThemeCard)
    .and(page.locator(`[data-theme-id="${helped.theme.id}"]`));
  const lastChange = card.getByTestId(ids.retroThemeCardLastChange);
  await expect(lastChange).toHaveText(/^Last change\s*Helped$/);
  // A theme with no shipped change has no Last change line.
  const none = page
    .getByTestId(ids.retroThemeCard)
    .and(page.locator(`[data-theme-id="${recurring.theme.id}"]`));
  await expect(none).toHaveCount(1);
  await expect(none.getByTestId(ids.retroThemeCardLastChange)).toHaveCount(0);
  const shipped = base.themes.filter((row) => row.lastVerdict !== null).length;
  await expect(page.getByTestId(ids.retroThemeCardLastChange)).toHaveCount(shipped);
});

test('ThemeCards_WhenNoPointHasNoSprint_ShouldLeaveOutTheNoSprintLine', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open());
  const cards = page.getByTestId(ids.retroThemeCard);
  await expect(cards).toHaveCount(base.themes.length);
  expect(base.themes.every((row) => row.noSprint === 0)).toBe(true);
  await expect(page.getByTestId(ids.retroHeatNoSprint)).toHaveCount(0);
  await expect(cards.first().getByTestId(ids.retroHeatTotal)).toHaveText(
    String(base.themes[0].total),
  );
});

test('RenameTheme_ShouldShowTheHelperLine', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ThemesAddressHarness insights={base} />, open(sheetQuery(helped.theme.id)));
  await page.getByTestId(ids.retroThemeSheetRename).tap();
  const field = page.getByTestId(ids.retroThemeRenameField);
  const helper = page.getByTestId(ids.retroThemeRenameHelper);
  await expect(helper).toHaveText(
    'Renaming keeps every point, action item and count. Agents see the new name in their next brief.',
  );
  await expect(field).toHaveAttribute('aria-describedby', (await helper.getAttribute('id'))!);
  // The helper sits under the field, and the rest of the sheet stays under them.
  const status = page.getByTestId(ids.retroThemeSheetStatus);
  await expect(status).toBeVisible();
  const [fieldBox, helperBox, statusBox] = await Promise.all([
    field.boundingBox(),
    helper.boundingBox(),
    status.boundingBox(),
  ]);
  expect(helperBox!.y).toBeGreaterThanOrEqual(fieldBox!.y + fieldBox!.height);
  expect(statusBox!.y).toBeGreaterThanOrEqual(helperBox!.y + helperBox!.height);
  // Rename… and Merge give way to Discard and Save while the name is being changed.
  await expect(page.getByTestId(ids.retroThemeSheetRename)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroThemeSheetMerge)).toHaveCount(0);
});

test('ThemeSheet_WhenAThemeThenTheUnthemedSheetOpen_ShouldReportEachOpeningWithItsSheet', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const opened: unknown[] = [];
  await page.route('**/api/projects/*/usage/retro-theme-sheet-opened', (route) => {
    opened.push(route.request().postDataJSON());
    return route.fulfill({ status: 204 });
  });
  await mount(<ThemesAddressHarness insights={withUnthemed} />, open('?range=6'));
  const card = page
    .getByTestId(ids.retroThemeCard)
    .and(page.locator(`[data-theme-id="${helped.theme.id}"]`));
  await card.getByTestId(ids.retroHeatStrip).tap();
  await expect(page.getByTestId(ids.retroThemeSheet)).toBeVisible();
  await expect.poll(() => opened).toEqual([{ sheet: 'theme' }]);
  // The sheet's own loads arriving add no second report.
  await expect(page.getByTestId(ids.retroThemeSheetMention)).toHaveCount(3);
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);

  await page.getByTestId(ids.retroInsightsUnthemed).getByTestId(ids.retroHeatStrip).tap();
  await expect(page.getByTestId(ids.retroUnthemedSheet)).toBeVisible();

  await expect.poll(() => opened).toEqual([{ sheet: 'theme' }, { sheet: 'unthemed' }]);
});
