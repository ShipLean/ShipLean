import { expect } from '@playwright/experimental-ct-react';
import { stubFeatureBoard } from '../../features/featureBoard/ct/stubFeatureBoard';
import { taskboardBoard, stubSettings } from '../../features/taskboardSettings/ct/stubSettings';
import { taskboardTestIds } from '../../features/taskboard/testIds';
import { FEATURE_BOARD_VIEW } from './fixtures/featureBoardView';
import { TASKBOARD_CARD_FIELDS, TASKBOARD_STYLE_RULES } from './fixtures/taskboardView';
import { pageShots, type Page } from './shoot';
import { stubShell, SHELL_MEMBERS, SHELL_PROJECT, SHELL_SPRINTS } from './shellStub';

const ROUTE = '/p/SHIP/items/SHIP-101/board';
const tags = ['Blocked', 'Spike', 'Tech debt'];

async function arrange(page: Page, saved?: unknown) {
  await stubShell(page);
  await stubFeatureBoard(page, FEATURE_BOARD_VIEW, { saved });
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

pageShots([
  { name: 'feature-board', route: ROUTE, shell: true, arrange: (p) => arrange(p), ready },
]);
