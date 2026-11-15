import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { touchTestIds } from '../../../touch/testIds';
import { retroSearch, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { RetrosSearchPanel } from './RetrosSearchPanel';

// Retrospectives › Search on a phone (SHIP-730): the search row, the three-line result rows, the Filter sheet, the filter
// chips, No results, and the tablet (768px gets the phone layout). Regenerate the baselines with
// scripts/update-page-shots.sh src/features/retros/search/Search.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const route = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/search${query}` } });
const theme = retroSearch.themes[0];
const logOnlyId = retroSearch.logMatchPointIds[0];

for (const colour of THEMES) {
  test(`SearchPhone_WhenOpened_ShouldMatchItsScreenshot_retro-search-phone_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    const screen = await mount(<RetrosSearchPanel project={project} />, route());
    await expect(page.getByTestId(ids.retrosRow)).toHaveCount(retroSearch.rows.length);
    await expect(screen).toHaveScreenshot(`retro-search-phone.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`SearchPhone_WhenFilterOpened_ShouldMatchItsScreenshot_retro-search-phone-filter_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(
      <RetrosSearchPanel project={project} />,
      route('?category=wentBadly&unthemed=true'),
    );
    await page.getByTestId(ids.retrosFilterOpen).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toBeVisible();
    await expect(sheet).toHaveScreenshot(`retro-search-phone-filter.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`SearchPhone_WhenFiltersAreOn_ShouldMatchItsScreenshot_retro-search-phone-chips_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    const screen = await mount(
      <RetrosSearchPanel project={project} />,
      route(`?category=wentBadly&theme=${theme.id}&unthemed=true`),
    );
    await expect(page.getByTestId(ids.retrosChip)).toHaveCount(3);
    await expect(screen).toHaveScreenshot(`retro-search-phone-chips.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`SearchPhone_WhenNothingMatches_ShouldMatchItsScreenshot_retro-search-phone-empty_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await serveEmpty(page);
    const screen = await mount(
      <RetrosSearchPanel project={project} />,
      route('?q=zebra&category=wentWell'),
    );
    await expect(page.getByTestId(ids.retrosEmpty)).toBeVisible();
    await expect(screen).toHaveScreenshot(`retro-search-phone-empty.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`SearchTablet_WhenOpened_ShouldMatchItsScreenshot_retro-search-tablet_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    const screen = await mount(<RetrosSearchPanel project={project} />, route());
    await expect(page.getByTestId(ids.retrosRow)).toHaveCount(retroSearch.rows.length);
    await expect(screen).toHaveScreenshot(`retro-search-tablet.${colour}.png`, {
      animations: 'disabled',
    });
  });
}

async function serveEmpty(page: Parameters<typeof stubRetros>[0]) {
  const empty = { ...retroSearch, rows: [], total: 0, logMatchPointIds: [], selected: null };
  await page.route(/\/api\/projects\/[^/]+\/retros(\?.*)?$/, (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(empty) }),
  );
}

test('SearchPhone_WhenRowsShow_ShouldBeThreeLineButtonsWithTheMatchParts', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const word = retroSearch.rows[0].rootCause!.split(' ')[3];
  await mount(<RetrosSearchPanel project={project} />, route(`?q=${word}`));
  const rows = page.getByTestId(ids.retrosRow);
  await expect(rows).toHaveCount(retroSearch.rows.length);
  await expect(rows.first().getByTestId(ids.retrosRowRootCause)).toContainText('Root cause:');
  await expect(rows.nth(1).getByTestId(ids.retrosRowRootCause)).toHaveCount(0);
  const logRow = page.locator(`[data-point-id="${logOnlyId}"]`);
  await expect(logRow.getByTestId(ids.retrosRowLogMatch)).toHaveText(
    'Match in the raw failure log',
  );
  const box = await rows.first().boundingBox();
  expect(box!.width).toBeGreaterThan(380);
  expect(box!.height).toBeGreaterThanOrEqual(32);
});

test('SearchPhone_WhenOpened_ShouldKeepTheSearchFieldAlone16pxAndNothingSticky', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RetrosSearchPanel project={project} />, route());
  const box = page.getByTestId(ids.retrosSearchBox);
  await expect(box).toHaveAttribute('enterkeyhint', 'search');
  expect(await box.evaluate((el) => getComputedStyle(el).fontSize)).toBe('16px');
  expect((await box.boundingBox())!.width).toBeGreaterThan(350);
  const sticky = await page
    .getByTestId(ids.retrosSearchPanel)
    .evaluate(
      (panel) =>
        [...panel.querySelectorAll('*')].filter(
          (el) => getComputedStyle(el).position === 'sticky' && el.getClientRects().length > 0,
        ).length,
    );
  expect(sticky).toBe(0);
  await expect(page.getByTestId(ids.retrosCount)).toHaveText(
    `${retroSearch.total} points in 4 retros`,
  );
});

test('SearchPhone_WhenEnterIsPressed_ShouldCloseTheKeyboard', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetrosSearchPanel project={project} />, route());
  const box = page.getByTestId(ids.retrosSearchBox);
  await box.focus();
  await box.press('Enter');
  await expect(box).not.toBeFocused();
});

test('SearchPhone_WhenAFilterIsRemoved_ShouldWriteTheAddressAndShowClearAllWithTwo', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RetrosSearchPanel project={project} />, route('?category=wentBadly&unthemed=true'));
  const chips = page.getByTestId(ids.retrosChip);
  await expect(chips).toHaveCount(2);
  await expect(page.getByTestId(ids.retrosChipsClear)).toBeVisible();
  await chips.first().getByTestId(ids.retrosChipRemove).tap();
  await expect(chips).toHaveCount(1);
  await expect(page.getByTestId(ids.retrosChipsClear)).toHaveCount(0);
  await expect(page.getByTestId(ids.retrosFilterOpen)).toHaveAttribute('data-count', '1');
});

test('SearchPhone_WhenOpenedWithATheme_ShouldShowThatThemesChip', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetrosSearchPanel project={project} />, route(`?theme=${theme.id}`));
  await expect(page.getByTestId(ids.retrosChip)).toHaveText(`# ${theme.name}✕`);
});

test('SearchPhone_WhenAFilterIsPickedInTheSheet_ShouldApplyAtOnce', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetrosSearchPanel project={project} />, route());
  await page.getByTestId(ids.retrosFilterOpen).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await sheet.locator('[data-filter="category"][data-value="wentWell"]').tap();
  await sheet.getByTestId(ids.retrosFilterUnthemed).tap();
  await expect(sheet.getByTestId(ids.retrosFilterUnthemed)).toHaveAttribute('aria-checked', 'true');
  await expect(sheet.getByTestId(ids.retrosFilterShow)).toHaveText(
    `Show ${retroSearch.total} points`,
  );
  await sheet.getByTestId(ids.retrosFilterShow).tap();
  await expect(sheet).toHaveCount(0);
  await expect(page.getByTestId(ids.retrosChip)).toHaveCount(2);
});

test('SearchPhone_WhenNothingMatches_ShouldOfferClearSearchAndFilters', async ({ mount, page }) => {
  await stubRetros(page);
  await serveEmpty(page);
  await mount(<RetrosSearchPanel project={project} />, route('?q=zebra&category=wentWell'));
  await expect(page.getByTestId(ids.retrosEmpty)).toContainText('zebra');
  await page.getByTestId(ids.retrosEmptyClear).tap();
  await expect(page.getByTestId(ids.retrosChip)).toHaveCount(0);
  await expect(page.getByTestId(ids.retrosSearchBox)).toHaveValue('');
});

test('SearchPhone_WhenCleared_ShouldKeepTheSearchTextFromTheSheetAndTheChipsButNotFromNoResults', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(
    <RetrosSearchPanel project={project} />,
    route('?q=brief&category=wentBadly&unthemed=true'),
  );
  const box = page.getByTestId(ids.retrosSearchBox);
  // The search text is not a filter: two filters count, the text does not.
  await expect(page.getByTestId(ids.retrosFilterOpen)).toHaveAttribute('data-count', '2');
  await page.getByTestId(ids.retrosChipsClear).tap();
  await expect(page.getByTestId(ids.retrosChip)).toHaveCount(0);
  await expect(box).toHaveValue('brief');

  await page.getByTestId(ids.retrosFilterOpen).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await sheet.locator('[data-filter="category"][data-value="wentWell"]').tap();
  await expect(page.getByTestId(ids.retrosChip)).toHaveCount(1);
  await sheet.getByTestId(ids.retrosFilterSheetClear).tap();
  await expect(page.getByTestId(ids.retrosChip)).toHaveCount(0);
  await expect(box).toHaveValue('brief');
});
