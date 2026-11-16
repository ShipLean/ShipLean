import { expect } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { componentTestIds } from '../../components/testIds';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { stubSprintsList, sprintsWithGoals } from './fixtures/sprints';
import { phonePageShots } from './shootPhone';
import { PEOPLE, stubShell } from './shellStub';
import { touchTestIds } from '../../touch/testIds';

// All sprints on a phone (SHIP-723, L723a; ND1 SHIP-1846 against design/mobile/sprints-review.html): the head and the chart,
// the sections scrolled into view, the ⋯ sheet of the current and of a past sprint, the empty project, and the New sprint
// form full screen.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/sprints.phone.spec.tsx.

const route = '/p/SHIP/sprints';
const arrange = async (page: Page) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubSprintsList(page);
  // Sprint 21 has a member's capacity note (SHIP-1807 P8: its own line under the figures).
  await page.route('**/api/sprints/sprint-21/capacity', (r) =>
    r.fulfill({
      json: {
        sprintId: 'sprint-21',
        members: [
          { user: PEOPLE.priya, days: null, points: 16, note: 'Mei and Priya at a conference' },
        ],
        teamTotal: 16,
        velocity: null,
        velocityDefault: 0,
        workingDays: 10,
        defaultDays: 10,
        holidays: [],
        holidayCountry: null,
        ended: true,
      },
    }),
  );
};
const openSheetOf = (name: string) => async (page: Page) => {
  await page
    .getByTestId(ids.listRow)
    .filter({ has: page.getByTestId(ids.rowLink).filter({ hasText: new RegExp(`^${name}$`) }) })
    .getByTestId(touchTestIds.moreButton)
    .tap();
  await expect(page.getByTestId(touchTestIds.actionSheetRow).first()).toBeVisible();
};
const ready = async (page: Page) => {
  await expect(page.getByTestId(ids.listRow).first()).toBeVisible();
  await expect(page.getByTestId(ids.velocityBar).first()).toBeVisible();
  await expect(page.getByTestId(ids.rowCommitted).first()).not.toHaveText('');
};

phonePageShots([
  { name: 'sprints', route, shell: true, arrange, ready },
  {
    name: 'sprints-list',
    route,
    shell: true,
    arrange,
    ready: async (page) => {
      await ready(page);
      await expect(page.getByTestId(ids.rowNotes)).toBeVisible();
    },
    act: async (page) => {
      const current = page.getByTestId(ids.listSection).first();
      await current.evaluate((el) => el.scrollIntoView({ block: 'start' }));
      await expect(current).toBeInViewport();
    },
  },
  { name: 'sprints-more', route, shell: true, arrange, ready, act: openSheetOf('Sprint 24') },
  { name: 'sprints-more-past', route, shell: true, arrange, ready, act: openSheetOf('Sprint 21') },
  {
    name: 'sprints-empty',
    route,
    shell: true,
    arrange: async (page) => {
      await stubShell(page, { sprints: [] });
      await stubSprintsList(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(ids.listEmpty)).toBeVisible();
    },
  },
  {
    name: 'sprints-new',
    route,
    // The full-screen dialog covers the live dot, so there is nothing to mask or check.
    shell: false,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.newSprintOpen).tap();
      await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
      await expect(page.getByTestId(ids.newSprintName)).not.toHaveValue('');
    },
  },
]);
