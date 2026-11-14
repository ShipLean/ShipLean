// The Feature board on a phone (SHIP-722): one state a page, lanes on every page, and a task's ⋯ sheet. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/featureBoard.phone.spec.tsx.
import type { FeatureBoardViewDto } from '@shiplean/core/api/views';
import { expect } from '@playwright/experimental-ct-react';
import { stubFeatureBoard } from '../../features/featureBoard/ct/stubFeatureBoard';
import { taskboardBoard, stubSettings } from '../../features/taskboardSettings/ct/stubSettings';
import { taskboardTestIds } from '../../features/taskboard/testIds';
import { touchTestIds } from '../../touch/testIds';
import { FEATURE_BOARD_PARENT_VIEW, FEATURE_BOARD_VIEW } from './fixtures/featureBoardView';
import { TASKBOARD_CARD_FIELDS, TASKBOARD_STYLE_RULES } from './fixtures/taskboardView';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell, SHELL_MEMBERS, SHELL_PROJECT, SHELL_SPRINTS } from './shellStub';

const ROUTE = '/p/SHIP/items/SHIP-101/board';
const tags = ['Blocked', 'Spike', 'Tech debt'];

async function arrange(page: Page, view: FeatureBoardViewDto, saved?: unknown) {
  await stubShell(page);
  await stubFeatureBoard(page, view, { saved });
  // The Taskboard stub treats every preference PUT as its own `view:taskboard`; the shell's recent-projects write must not.
  await page.route('**/api/me/preferences/recentProjects', (r) => r.fulfill({ status: 204 }));
  await stubSettings(
    page,
    taskboardBoard({ styleRules: TASKBOARD_STYLE_RULES, cardFields: TASKBOARD_CARD_FIELDS }),
    tags,
    SHELL_PROJECT.id,
  );
  // The settings stub answers empty sprints and members; the shell's own must win again.
  await page.route(`**/api/projects/${SHELL_PROJECT.id}/sprints`, (r) =>
    r.fulfill({ json: SHELL_SPRINTS }),
  );
  await page.route(`**/api/projects/${SHELL_PROJECT.id}/members`, (r) =>
    r.fulfill({ json: SHELL_MEMBERS }),
  );
}

const ready = (page: Page) => expect(page.getByTestId(taskboardTestIds.card).first()).toBeVisible();

const PARENT_ROUTE = '/p/SHIP/items/SHIP-103/board';
const people = { groupBy: 'people' };

const chip = (page: Page, name: string) =>
  page
    .getByTestId(touchTestIds.pagerChip)
    .filter({ hasText: new RegExp(`^\\s*${name}(?![A-Za-z])`) });

phonePageShots([
  {
    name: 'feature-board',
    route: ROUTE,
    shell: true,
    arrange: (p) => arrange(p, FEATURE_BOARD_VIEW),
    ready,
  },
  {
    name: 'feature-board-active',
    route: ROUTE,
    shell: true,
    arrange: (p) => arrange(p, FEATURE_BOARD_VIEW),
    ready,
    act: async (page) => {
      await chip(page, 'Active').tap();
      await expect(chip(page, 'Active')).toHaveAttribute('aria-current', 'true');
      // The smooth scroll has settled once the Active page's header is wholly on screen.
      await expect(
        page
          .getByTestId(taskboardTestIds.phonePageHead)
          .filter({ hasText: /^\s*Active/ })
          .first(),
      ).toBeInViewport({ ratio: 1 });
    },
  },
  {
    name: 'feature-board-parent',
    route: PARENT_ROUTE,
    shell: true,
    arrange: (p) => arrange(p, FEATURE_BOARD_PARENT_VIEW),
    ready,
  },
  {
    name: 'feature-board-people',
    route: PARENT_ROUTE,
    shell: true,
    arrange: (p) => arrange(p, FEATURE_BOARD_PARENT_VIEW, people),
    ready,
  },
  {
    name: 'feature-board-task-actions',
    route: ROUTE,
    shell: true,
    arrange: (p) => arrange(p, FEATURE_BOARD_VIEW),
    ready,
    act: async (page) => {
      await page.getByTestId(taskboardTestIds.cardActions).first().tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    },
  },
]);
