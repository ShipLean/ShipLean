import { expect } from '@playwright/experimental-ct-react';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { touchTestIds } from '../../touch/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';
import { SPRINT_24, sprintsWithGoals, stubCapacity } from './fixtures/sprints';

// Sprint Capacity on a phone (SHIP-726, L726a; SHIP-1808 design): whole page, with a draft (the Save bar), the toolbar's ⋯
// sheet and nobody entered. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/capacity.phone.spec.tsx.

const emptyCapacity = {
  sprintId: SPRINT_24,
  members: [],
  teamTotal: 0,
  velocity: null,
  velocityDefault: 25,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
};
const arrange = async (page: Parameters<typeof stubShell>[0]) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
};
const ready = async (page: Parameters<typeof stubShell>[0]) => {
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(5);
  await expect(page.getByTestId(ids.capacityCallout)).toBeVisible();
};

phonePageShots([
  {
    name: 'capacity',
    route: `/p/SHIP/sprints/${SPRINT_24}/capacity`,
    shell: true,
    arrange,
    ready,
  },
  {
    name: 'capacity-editing',
    route: `/p/SHIP/sprints/${SPRINT_24}/capacity`,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.capacityDays).first().fill('6');
      await expect(page.getByTestId(ids.capacityTotal)).not.toHaveText('21.5');
    },
  },
  {
    name: 'capacity-more',
    route: `/p/SHIP/sprints/${SPRINT_24}/capacity`,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.capacityPhoneToolbar).getByTestId(touchTestIds.moreButton).tap();
      await expect(
        page.getByTestId(touchTestIds.sheet).getByTestId(touchTestIds.actionSheetRow).first(),
      ).toHaveText('Clear');
    },
  },
  {
    name: 'capacity-empty',
    route: `/p/SHIP/sprints/${SPRINT_24}/capacity`,
    shell: true,
    arrange: async (page) => {
      await arrange(page);
      await page.route(`**/api/sprints/${SPRINT_24}/capacity`, (route) =>
        route.fulfill({ json: emptyCapacity }),
      );
    },
    ready: async (page) => {
      await expect(page.getByTestId(ids.capacityEmpty)).toHaveText(
        'No capacity entered: planning uses velocity 21',
      );
    },
  },
]);
