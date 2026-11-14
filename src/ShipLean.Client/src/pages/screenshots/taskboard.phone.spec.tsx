import { expect } from '@playwright/experimental-ct-react';
import type { MemberDto } from '@shiplean/core/api/projects';
import { taskboardBoard, stubSettings } from '../../features/taskboardSettings/ct/stubSettings';
import { stubTaskboard } from '../../features/taskboard/ct/stubApi';
import { taskboardTestIds } from '../../features/taskboard/testIds';
import { touchTestIds } from '../../touch/testIds';
import {
  TASKBOARD_CARD_FIELDS,
  TASKBOARD_STYLE_RULES,
  TASKBOARD_VIEW,
} from './fixtures/taskboardView';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';
import { stubShell, SHELL_MEMBERS, SHELL_PROJECT, SHELL_SPRINTS } from './shellStub';

// The sprint Taskboard on a phone (SHIP-725): the pager with its lanes, grouped by People with everyone on the project, and
// a task's ⋯ sheet. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/taskboard.phone.spec.tsx.

const ROUTE = '/p/SHIP/sprints/sprint-24/taskboard';

// Someone on the project with no task in the sprint: the People grouping still gives them a lane.
const NO_TASKS: MemberDto = {
  user: {
    id: 'u-jo-park',
    name: 'Jo Park',
    initials: 'JP',
    avatarColour: '#8a5ab5',
    photoUrl: null,
  },
  email: 'jo.park@larkspur.dev',
  role: 'member',
  status: 'active',
};

async function arrange(page: Page, saved?: unknown) {
  await stubShell(page);
  await stubTaskboard(page, TASKBOARD_VIEW, { saved });
  // The Taskboard stub treats every preference PUT as its own `view:taskboard`; the shell's recent-projects write must not.
  await page.route('**/api/me/preferences/recentProjects', (r) => r.fulfill({ status: 204 }));
  await stubSettings(
    page,
    taskboardBoard({ styleRules: TASKBOARD_STYLE_RULES, cardFields: TASKBOARD_CARD_FIELDS }),
    ['Blocked', 'Spike', 'Tech debt'],
    SHELL_PROJECT.id,
  );
  // The settings stub answers empty sprints and members; the shell's own must win again.
  await page.route(`**/api/projects/${SHELL_PROJECT.id}/sprints`, (r) =>
    r.fulfill({ json: SHELL_SPRINTS }),
  );
  await page.route(`**/api/projects/${SHELL_PROJECT.id}/members`, (r) =>
    r.fulfill({ json: [...SHELL_MEMBERS, NO_TASKS] }),
  );
}

const ready = (page: Page) => expect(page.getByTestId(taskboardTestIds.card).first()).toBeVisible();

phonePageShots([
  { name: 'taskboard', route: ROUTE, shell: true, arrange: (p) => arrange(p), ready },
  {
    name: 'taskboard-people',
    route: ROUTE,
    shell: true,
    arrange: (p) => arrange(p, { groupBy: 'people' }),
    ready: async (page) => {
      await expect(page.getByTestId(taskboardTestIds.lane).first()).toHaveAttribute(
        'data-row',
        'unassigned',
      );
      await expect(page.getByTestId(taskboardTestIds.phonePersonEmpty).first()).toBeVisible();
    },
  },
  {
    name: 'taskboard-task-actions',
    route: ROUTE,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (page) => {
      await page.getByTestId(taskboardTestIds.cardActions).first().tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    },
  },
]);
