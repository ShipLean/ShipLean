import type { Page } from 'playwright';
import { expect } from '@playwright/experimental-ct-react';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { touchTestIds } from '../../touch/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';
import { SPRINT_24, sprintsWithGoals, stubSprintBacklog } from './fixtures/sprints';

// The Sprint backlog's figures, burndown and glance rows on a phone (SHIP-724, L724b): the page, the burndown read at a
// day, and the two glance sheets. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/sprintBacklog.phone.spec.tsx.

const backlog = {
  route: `/p/SHIP/sprints/${SPRINT_24}/backlog`,
  shell: true,
  arrange: async (page: Page) => {
    await stubShell(page, { sprints: sprintsWithGoals });
    await stubSprintBacklog(page);
  },
  ready: async (page: Page) => {
    await expect(page.getByTestId(ids.kpiCommitted)).toHaveText(/21/);
    await expect(page.getByTestId(ids.burndownPoint)).toHaveCount(5);
    await expect(page.getByTestId(ids.glanceReady)).toBeVisible();
  },
};

phonePageShots([
  { name: 'sprint-backlog', ...backlog },
  {
    name: 'sprint-backlog-burndown',
    ...backlog,
    act: async (page) => {
      const dot = page.getByTestId(ids.burndownPoint).nth(2);
      const x = Number(await dot.getAttribute('cx'));
      const y = Number(await dot.getAttribute('cy'));
      await page.getByTestId(ids.burndown).tap({ position: { x, y } });
      await expect(page.getByTestId(ids.burndownReadout)).toHaveText(
        '21 Oct · 19 left · ideal 16.3',
      );
    },
  },
  {
    name: 'sprint-backlog-ready',
    ...backlog,
    act: async (page) => {
      await page.getByTestId(ids.glanceReady).tap();
      await expect(
        page.getByTestId(touchTestIds.sheet).getByTestId(ids.glanceReadyItem),
      ).toHaveCount(3);
    },
  },
  {
    name: 'sprint-backlog-active',
    ...backlog,
    act: async (page) => {
      await page.getByTestId(ids.glanceActive).tap();
      await expect(page.getByTestId(touchTestIds.sheet).getByTestId(ids.glancePerson)).toHaveCount(
        5,
      );
    },
  },
]);
