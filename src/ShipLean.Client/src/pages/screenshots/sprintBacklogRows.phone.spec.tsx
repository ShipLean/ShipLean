import type { Page } from 'playwright';
import { expect } from '@playwright/experimental-ct-react';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';
import {
  SPRINT_24,
  sprintItems,
  sprintsWithGoals,
  states,
  stubSprintBacklog,
} from './fixtures/sprints';

// The Sprint backlog list on a phone (SHIP-724, L724c): the page, the States sheet, a state conflict (toast and flashing
// row) and the list with Tasks off. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/sprintBacklogRows.phone.spec.tsx.

const backlog = {
  route: `/p/SHIP/sprints/${SPRINT_24}/backlog`,
  shell: true,
  arrange: async (page: Page) => {
    await stubShell(page, { sprints: sprintsWithGoals });
    await stubSprintBacklog(page);
  },
  ready: async (page: Page) => {
    await expect(page.getByTestId(ids.storyRow)).toHaveCount(5);
  },
};

phonePageShots([
  { name: 'sprint-backlog-list', ...backlog },
  {
    name: 'sprint-backlog-state',
    ...backlog,
    act: async (page) => {
      await page.getByTestId(ids.storyRow).first().getByTestId(ids.stateButton).tap();
      await expect(page.getByTestId(ids.statesSheetOption)).toHaveCount(4);
    },
  },
  {
    name: 'sprint-backlog-conflict',
    ...backlog,
    arrange: async (page) => {
      await backlog.arrange(page);
      // The server answers every state change with a conflict: the teammate got there first.
      await page.route('**/api/work-items/*', (route) =>
        route.fulfill({
          status: 409,
          json: {
            error: 'changed',
            code: 'conflict',
            current: sprintItems.find((i) => i.key === 'SHIP-141'),
            theirChanges: [],
          },
        }),
      );
    },
    act: async (page) => {
      const closed = states.find((s) => s.type === 'story' && s.name === 'Closed')!;
      await page.getByTestId(ids.storyRow).first().getByTestId(ids.stateButton).tap();
      await page.getByTestId(ids.statesSheetOption).filter({ hasText: closed.name }).tap();
      await expect(page.getByText('SHIP-141 was changed by someone else')).toBeVisible();
      await expect(page.getByTestId(ids.storyRow).first()).toHaveAttribute('data-flash', 'true');
    },
  },
  {
    name: 'sprint-backlog-no-tasks',
    ...backlog,
    act: async (page) => {
      await page.getByTestId(ids.showTasks).tap();
      await expect(page.getByTestId(ids.taskRow)).toHaveCount(0);
    },
  },
]);
