import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { sprintRetro } from '../../ct/retroFixtures';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { retrosTestIds as ids } from '../../testIds';
import { RetroRail } from './RetroRail';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;
const mountOptions = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const element = <RetroRail project={project} sprint={sprint} retro={sprintRetro} />;

test('RetroRail_WhenOpened_ShouldDescribeCoverageAndActionItemsAsTheCodeListsThem', async ({
  mount,
  page,
}) => {
  await mount(element, mountOptions);
  await expect(page.getByTestId(ids.retroCoverage)).toContainText(
    'Stories, bugs and Action items finished in the sprint, and Features with a child that finished or rolled over',
  );
  await expect(page.getByTestId(ids.retroRailActions)).toContainText(
    'plus other open ones on the same themes',
  );
});

test('RetroRail_WhenOpened_ShouldListCoverageWithDoneMissingAndWaiting', async ({
  mount,
  page,
}) => {
  await mount(element, mountOptions);
  const rows = page.getByTestId(ids.retroCoverage).getByTestId(ids.retroCoverageRow);
  await expect(rows).toHaveCount(sprintRetro.coverage.length);
  for (const [i, row] of sprintRetro.coverage.entries()) {
    await expect(rows.nth(i)).toHaveAttribute('data-status', row.status);
  }
  await expect(rows.filter({ hasText: '✓' }).first()).toBeVisible();
  await expect(page.getByTestId(ids.retroCoverage)).toContainText('missing');
  await expect(page.getByTestId(ids.retroCoverage)).toContainText('waiting');
});

test('RetroRail_WhenOpened_ShouldShowActionItemsWithKeyStateAndPlannedSprint', async ({
  mount,
  page,
}) => {
  await mount(element, mountOptions);
  const actions = page.getByTestId(ids.retroRailActions);
  for (const row of sprintRetro.actionItems) {
    const item = actions.locator(`[data-item-key="${row.action.key}"]`).first();
    await expect(item).toContainText(row.action.state.name);
    await expect(item.locator('[data-planned]')).toContainText(
      row.action.sprint?.name ?? 'Not planned',
    );
  }
});

test('RetroRail_WhenOpened_ShouldShowARecurringStripPerTheme', async ({ mount, page }) => {
  await mount(element, mountOptions);
  const recurring = page.getByTestId(ids.retroRailRecurring);
  await expect(recurring.getByTestId(shared.retroMentionStrip)).toHaveCount(
    sprintRetro.recurring.length,
  );
  const withFix = sprintRetro.recurring.filter((s) => s.fixAt !== null).length;
  await expect(recurring.locator('[data-fix]')).toHaveCount(withFix);
});

test('RetroRail_WhenItemKeysAreShown_ShouldLinkThemToTheItem', async ({ mount, page }) => {
  await mount(element, mountOptions);
  const key = sprintRetro.actionItems[0].action.key;
  await expect(
    page.getByTestId(ids.retroRailActions).locator(`a[data-item-key="${key}"]`),
  ).toHaveAttribute('href', `/p/SHIP/items/${key}`);
  const covered = sprintRetro.coverage[0].item.key;
  await expect(
    page.getByTestId(ids.retroCoverage).locator(`a[data-item-key="${covered}"]`),
  ).toHaveAttribute('href', `/p/SHIP/items/${covered}`);
});

for (const theme of ['light', 'dark'] as const) {
  test(`RetroRail_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const rail = await mount(element, mountOptions);
    await expect(rail).toHaveScreenshot(`retro-rail.${theme}.png`, { animations: 'disabled' });
  });
}
