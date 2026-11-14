import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroInsightsDto } from '@shiplean/core/api/retros';
import { retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { ActionsImpactHarness } from './touch/ct/ActionsImpactHarness';

// Retrospectives › Insights, Action items and Changes & impact, at 900px and below (SHIP-731, L731c). Regenerate the
// baselines with scripts/update-page-shots.sh src/features/retros/insights/ActionsImpact.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = (view: string) => ({
  hooksConfig: { route: `/p/SHIP/retros/insights?view=${view}` },
});
type Page = Parameters<typeof stubRetros>[0];

async function stubSprints(page: Page) {
  await page.route(
    (url) => /\/api\/projects\/[^/]+\/sprints$/.test(url.pathname),
    (r) =>
      r.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'sprint-25', name: 'Sprint 25', status: 'future', startDate: '2026-11-02' },
        ]),
      }),
  );
}

const isOpen = (category: string) => category !== 'completed' && category !== 'removed';

test('ActionItemsPhone_WhenOpened_ShouldListACardPerItemOpenFirstWithNoTable', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await stubSprints(page);
  // Done items first in the data, so only the view's own ordering can put the open ones on top.
  const doneFirst: RetroInsightsDto = {
    ...retroInsights,
    actionItems: [...retroInsights.actionItems].sort(
      (a, b) => Number(isOpen(a.action.state.category)) - Number(isOpen(b.action.state.category)),
    ),
  };
  await mount(<ActionsImpactHarness view="actions" insights={doneFirst} />, route('actions'));
  const cards = page.getByTestId(ids.retroActionCard);
  await expect(cards).toHaveCount(retroInsights.actionItems.length);
  await expect(page.getByTestId(ids.retroActionsTable)).toHaveCount(0);
  const keys = await cards.evaluateAll((els) => els.map((e) => e.getAttribute('data-item-key')));
  const open = retroInsights.actionItems.filter((r) => isOpen(r.action.state.category));
  const rest = retroInsights.actionItems.filter((r) => !isOpen(r.action.state.category));
  expect(open.length).toBeGreaterThan(0);
  expect(rest.length).toBeGreaterThan(0);
  expect(keys).toEqual([...open, ...rest].map((r) => r.action.key));
});

test('ActionItemsPhone_WhenOpened_ShouldGiveEachCardItsKeyTitleThemeAndALinkToTheItem', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await stubSprints(page);
  await mount(<ActionsImpactHarness view="actions" insights={retroInsights} />, route('actions'));
  const row = retroInsights.actionItems.find((r) => r.theme)!;
  const card = page
    .getByTestId(ids.retroActionCard)
    .and(page.locator(`[data-item-key="${row.action.key}"]`));
  await expect(card).toContainText(row.action.key);
  await expect(card).toContainText(row.action.title);
  await expect(card).toContainText(row.theme!.name);
  await expect(card.getByTestId(ids.retroActionCardLink)).toHaveAttribute(
    'href',
    `/p/SHIP/items/${row.action.key}`,
  );
  const link = (await card.getByTestId(ids.retroActionCardLink).boundingBox())!;
  expect(link.height).toBeGreaterThanOrEqual(32);
});

test('ActionItemsPhone_WhenAnItemHasNoSprint_ShouldOfferA32pxPullIntoTheNextSprint', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await stubSprints(page);
  await mount(<ActionsImpactHarness view="actions" insights={retroInsights} />, route('actions'));
  const pull = page.getByTestId(ids.retroActionPull).first();
  await expect(pull).toHaveText('Pull into the next sprint');
  expect((await pull.boundingBox())!.height).toBeGreaterThanOrEqual(32);
});

test('ActionItemsPhone_WhenNothingRaised_ShouldSayNothingHere', async ({ mount, page }) => {
  await stubRetros(page);
  await stubSprints(page);
  const insights: RetroInsightsDto = { ...retroInsights, actionItems: [] };
  const view = await mount(
    <ActionsImpactHarness view="actions" insights={insights} />,
    route('actions'),
  );
  await expect(view).toContainText('Nothing here.');
  await expect(page.getByTestId(ids.retroActionCard)).toHaveCount(0);
});

test('ImpactPhone_WhenABarIsTapped_ShouldOutlineItAndReadOutItsColumnAndCount', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ActionsImpactHarness view="impact" insights={retroInsights} />, route('impact'));
  const first = retroInsights.impact[0];
  const card = page.getByTestId(ids.retroImpactCard).first();
  await expect(card.getByTestId(ids.retroImpactReadout)).toHaveCount(0);
  const index = first.counts.findIndex((c) => c > 1);
  expect(index).toBeGreaterThanOrEqual(0);
  await card.getByTestId(ids.retroImpactBar).nth(index).tap();
  const label = retroInsights.columns[index].label.replace(/^Sprint\s+/, 'S');
  await expect(card.getByTestId(ids.retroImpactReadout)).toHaveText(
    `${label}: ${first.counts[index]} mentions`,
  );
  await expect(card.locator('[data-bar][data-selected]')).toHaveCount(1);
  await expect(card.locator('[data-bar]').nth(index)).toHaveAttribute('data-selected', '');
  await card.getByTestId(ids.retroImpactBar).nth(index).tap();
  await expect(card.getByTestId(ids.retroImpactReadout)).toHaveCount(0);
});

test('ImpactPhone_WhenOpened_ShouldKeepTheNumbersAndReplaceTheExplanationWithAButton', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const view = await mount(
    <ActionsImpactHarness view="impact" insights={retroInsights} />,
    route('impact'),
  );
  await expect(page.locator('[data-figure="before"]').first()).toBeVisible();
  await expect(page.locator('[data-figure="after"]').first()).toBeVisible();
  await expect(view).not.toContainText('after at most half of before');
  const how = page.getByTestId(ids.retroImpactHowVerdicts);
  expect((await how.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  await how.tap();
  await expect(page.getByTestId('location-probe')).toContainText('sheet=defs');
  await expect(page.getByTestId('location-probe')).toContainText('view=impact');
});

test('ImpactPhone_WhenOpened_ShouldShowOneCardARow', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ActionsImpactHarness view="impact" insights={retroInsights} />, route('impact'));
  const boxes = await page
    .getByTestId(ids.retroImpactCard)
    .evaluateAll((els) => els.map((e) => e.getBoundingClientRect().left));
  expect(new Set(boxes).size).toBe(1);
});

test('ImpactTablet_WhenOpenedAt640_ShouldShowTwoCardsARow', async ({ mount, page }) => {
  await page.setViewportSize({ width: 640, height: 1024 });
  await stubRetros(page);
  await mount(<ActionsImpactHarness view="impact" insights={retroInsights} />, route('impact'));
  const lefts = await page
    .getByTestId(ids.retroImpactCard)
    .evaluateAll((els) => els.map((e) => Math.round(e.getBoundingClientRect().left)));
  expect(new Set(lefts).size).toBe(2);
});

test('ImpactPhone_WhenOpenedAt590_ShouldShowOneCardARow', async ({ mount, page }) => {
  await page.setViewportSize({ width: 590, height: 1024 });
  await stubRetros(page);
  await mount(<ActionsImpactHarness view="impact" insights={retroInsights} />, route('impact'));
  const lefts = await page
    .getByTestId(ids.retroImpactCard)
    .evaluateAll((els) => els.map((e) => Math.round(e.getBoundingClientRect().left)));
  expect(new Set(lefts).size).toBe(1);
});

test('ImpactTablet_WhenOpenedAt768_ShouldShowTwoCardsARow', async ({ mount, page }) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await stubRetros(page);
  await mount(<ActionsImpactHarness view="impact" insights={retroInsights} />, route('impact'));
  const lefts = await page
    .getByTestId(ids.retroImpactCard)
    .evaluateAll((els) => els.map((e) => Math.round(e.getBoundingClientRect().left)));
  expect(new Set(lefts).size).toBe(2);
});

for (const theme of THEMES) {
  test(`ActionsImpactPhone_WhenOpened_ShouldMatchItsScreenshot_retro-insights-actions-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubRetros(page);
    await stubSprints(page);
    const view = await mount(
      <ActionsImpactHarness view="actions" insights={retroInsights} />,
      route('actions'),
    );
    await expect(page.getByTestId(ids.retroActionCard).first()).toBeVisible();
    await expect(view).toHaveScreenshot(`retro-insights-actions-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ActionsImpactPhone_WhenOpened_ShouldMatchItsScreenshot_retro-insights-impact-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubRetros(page);
    const view = await mount(
      <ActionsImpactHarness view="impact" insights={retroInsights} />,
      route('impact'),
    );
    const card = page.getByTestId(ids.retroImpactCard).first();
    await card.getByTestId(ids.retroImpactBar).nth(1).tap();
    await expect(card.getByTestId(ids.retroImpactReadout)).toBeVisible();
    await expect(view).toHaveScreenshot(`retro-insights-impact-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ActionsImpactTablet_WhenOpened_ShouldMatchItsScreenshot_retro-insights-impact-tablet_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubRetros(page);
    const view = await mount(
      <ActionsImpactHarness view="impact" insights={retroInsights} />,
      route('impact'),
    );
    await expect(page.getByTestId(ids.retroImpactCard).first()).toBeVisible();
    await expect(view).toHaveScreenshot(`retro-insights-impact-tablet.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
