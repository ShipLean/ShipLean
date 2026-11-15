import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT, treeRows, treeView } from '../../features/backlog/ct/fixtures';
import { FEATURE_TREE_ROWS, levelView } from '../../features/backlog/ct/levelFixtures';
import { LevelSlotHarness } from '../../features/backlog/ct/LevelSlotHarness';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { stubLevel } from '../../features/backlog/ct/stubLevels';
import { backlogTestIds as ids } from '../../features/backlog/testIds';
import { TreeMode } from '../../features/backlog/tree/TreeMode';
import { stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// Tree mode on a phone (SHIP-716, L716e), as element shots of this task's regions only, so the other backlog tasks' merges
// don't move them: a nested group's path header with the rows under it, and the Features tree. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/backlogTree.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

for (const theme of THEMES) {
  test(`BacklogTreePhone_WhenOpened_ShouldMatchItsScreenshot_backlogs-tree-group_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBacklog(page, treeView(treeRows().rows));
    const screen = await mount(<TreeMode project={PROJECT} />, {
      hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=tree' },
    });
    const group = screen
      .getByTestId(ids.treeGroup)
      .filter({ has: page.getByTestId(ids.treeGroupPath).getByText('Receipts') });
    await expect(group).toBeVisible();

    await expect(group).toHaveScreenshot(`backlogs-tree-group.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BacklogTreePhone_WhenOpened_ShouldMatchItsScreenshot_backlogs-features-tree_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubLevel(page, levelView('features', FEATURE_TREE_ROWS, 'tree'));
    const screen = await mount(<LevelSlotHarness project={PROJECT} />, {
      hooksConfig: { route: '/p/SHIP/backlogs/features?mode=tree' },
    });
    const tree = screen.getByTestId(ids.touchTree);
    await expect(tree.getByTestId(ids.row)).toHaveCount(4);

    await expect(tree).toHaveScreenshot(`backlogs-features-tree.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

// The Stories Tree as a whole page: Epic and Feature headers with their rows under them.
phonePageShots([
  {
    name: 'backlogs-tree',
    route: '/p/SHIP/backlogs/stories?mode=tree',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, treeView(treeRows().rows));
    },
    ready: (page) => expect(page.getByTestId(ids.row)).toHaveCount(6),
  },
]);
