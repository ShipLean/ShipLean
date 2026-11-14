import { expect } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { stubSprintsList, sprintsWithGoals } from './fixtures/sprints';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// All sprints on a phone (SHIP-723, L723a): the list with its toolbar, and the New sprint form full screen.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/sprints.phone.spec.tsx.

const route = '/p/SHIP/sprints';
const arrange = async (page: Parameters<typeof stubShell>[0]) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubSprintsList(page);
};
const ready = async (page: Parameters<typeof stubShell>[0]) => {
  await expect(page.getByTestId(ids.listRow).first()).toBeVisible();
  await expect(page.getByTestId(ids.velocityBar).first()).toBeVisible();
  await expect(page.getByTestId(ids.rowCommitted).first()).not.toHaveText('');
};

phonePageShots([
  { name: 'sprints', route, shell: true, arrange, ready },
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
