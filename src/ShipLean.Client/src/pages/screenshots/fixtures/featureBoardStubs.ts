import type { FeatureBoardViewDto } from '@shiplean/core/api/views';
import { expect } from '@playwright/experimental-ct-react';
import { stubFeatureBoard } from '../../../features/featureBoard/ct/stubFeatureBoard';
import { taskboardBoard, stubSettings } from '../../../features/taskboardSettings/ct/stubSettings';
import { taskboardTestIds } from '../../../features/taskboard/testIds';
import type { Page } from '../shoot';
import { stubShell, SHELL_MEMBERS, SHELL_PROJECT, SHELL_SPRINTS } from '../shellStub';
import { TASKBOARD_CARD_FIELDS, TASKBOARD_STYLE_RULES } from './taskboardView';

// What the Feature board's page shots (desktop and phone) share: the routes, the API stubs and the ready check.

export const FEATURE_BOARD_ROUTE = '/p/SHIP/items/SHIP-101/board';
export const FEATURE_BOARD_PARENT_ROUTE = '/p/SHIP/items/SHIP-103/board';
export const FEATURE_BOARD_PEOPLE = { groupBy: 'people' };
const tags = ['Blocked', 'Spike', 'Tech debt'];

export async function arrangeFeatureBoard(page: Page, view: FeatureBoardViewDto, saved?: unknown) {
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

export const featureBoardReady = (page: Page) =>
  expect(page.getByTestId(taskboardTestIds.card).first()).toBeVisible();
