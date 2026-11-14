import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { ByWorkItemHarness } from '../../../../ct/ByWorkItemHarness';
import { sprintRetro, stubRetros } from '../../../../ct/retroFixtures';
import { retroSharedTestIds as shared } from '../../../../../retroShared/testIds';
import { retrosTestIds as ids } from '../../../../testIds';

// By work item on a phone (SHIP-727 D4): one collapsed card per item, a tap opens its notes.
// Regenerate the shots with scripts/update-page-shots.sh src/features/retros/sprint/views/byWorkItem/ct/ByWorkItem.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;
const mountOptions = { hooksConfig: { route: '/p/SHIP/sprints/sprint-24/retro?view=item' } };
const element = <ByWorkItemHarness project={project} sprint={sprint} retro={sprintRetro} />;

const phoneOnly = (page: { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone specs run only in the CT phone project').toBe(390);

test('ByWorkItem_WhenOnPhone_ShouldShowCollapsedCardsWithCounts', async ({ mount, page }) => {
  phoneOnly(page);
  await stubRetros(page);
  await mount(element, mountOptions);
  const cards = page.getByTestId(shared.retroCard);
  await expect(cards).toHaveCount(sprintRetro.cards.length);
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(0);
  await expect(page.getByTestId(shared.retroBoard)).toHaveCount(0);
  const toggles = page.getByTestId(ids.retroCardToggle);
  await expect(toggles.first()).toHaveAttribute('aria-expanded', 'false');

  // SHIP-141: one point in each of three categories, two still to decide (Went badly, Not in the plan).
  const story = cards.filter({ hasText: 'SHIP-141' });
  const counts = story.getByTestId(ids.retroCardCount);
  await expect(counts).toHaveCount(4);
  await expect(counts.nth(0)).toHaveAttribute('aria-label', 'Went well 1');
  await expect(story.locator('[data-category="notInPlan"]')).toHaveAttribute(
    'aria-label',
    'Not in the plan 1',
  );
  await expect(story.getByTestId(ids.retroCardToDecide)).toHaveText('2 to decide');
  await expect(story.getByTestId(ids.retroCardProgress)).toBeVisible();
  const box = (await story.getByTestId(ids.retroCardToggle).boundingBox())!;
  expect(box.height).toBeGreaterThanOrEqual(48);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);

  for (const theme of THEMES) {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await expect(page.getByTestId(ids.retroCardList)).toHaveScreenshot(
      `retro-items-phone.${theme}.png`,
      { animations: 'disabled' },
    );
  }
});

test('ByWorkItem_WhenCardOpened_ShouldHideEmptyCategories', async ({ mount, page }) => {
  phoneOnly(page);
  await stubRetros(page);
  await mount(element, mountOptions);
  // SHIP-129 has only Went badly points.
  const card = page.getByTestId(shared.retroCard).filter({ hasText: 'SHIP-129' });
  await card.getByTestId(ids.retroCardToggle).click();
  await expect(card.getByTestId(ids.retroCardToggle)).toHaveAttribute('aria-expanded', 'true');
  const columns = card.getByTestId(shared.retroBoardColumn);
  await expect(columns).toHaveCount(1);
  await expect(columns).toHaveAttribute('data-category', 'wentBadly');
  await expect(card.getByTestId(shared.retroNote)).toHaveCount(1);

  // Its raw failure log stays collapsed until tapped, and the retro link goes to the item's retro page.
  await expect(card.getByTestId(shared.retroCardLog)).toHaveCount(0);
  await card.getByTestId(shared.retroCardLogToggle).click();
  await expect(card.getByTestId(shared.retroCardLog)).toBeVisible();
  await expect(card.getByTestId(ids.retroCardRetroLink)).toHaveText('Open SHIP-129 retro ›');
  await expect(card.getByTestId(ids.retroCardRetroLink)).toHaveAttribute(
    'href',
    '/p/SHIP/retros/items/SHIP-129',
  );

  // A card with every category but Not in the plan shows three columns.
  const story = page.getByTestId(shared.retroCard).filter({ hasText: 'SHIP-141' });
  await story.getByTestId(ids.retroCardToggle).click();
  await expect(story.getByTestId(shared.retroBoardColumn)).toHaveCount(4);

  for (const theme of THEMES) {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await expect(story).toHaveScreenshot(`retro-item-open-phone.${theme}.png`, {
      animations: 'disabled',
    });
  }
});
