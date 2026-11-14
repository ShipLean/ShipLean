import { expect } from '@playwright/experimental-ct-react';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';
import { SPRINT_24, sprintsWithGoals, stubCapacity } from './fixtures/sprints';

// Sprint Capacity on a phone (SHIP-726, L726a): whole page, then with a draft (the Save bar). Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/capacity.phone.spec.tsx.

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
]);
