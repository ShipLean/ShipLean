import { expect } from '@playwright/experimental-ct-react';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { touchTestIds } from '../../touch/testIds';
import { phonePageShots } from './shootPhone';
import { PEOPLE, stubShell } from './shellStub';
import {
  SPRINT_24,
  capacitySprint24,
  emptyCapacity,
  sprintsWithGoals,
  stubCapacity,
} from './fixtures/sprints';

// Sprint Capacity on a phone (SHIP-726, L726a; SHIP-1808 design): whole page, with a draft (the Save bar), the toolbar's ⋯
// sheet, a person's days sheet, the Add member sheet and nobody entered. Regenerate with
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
      await page.getByTestId(ids.capacityRow).first().tap();
      await page.getByTestId(touchTestIds.sheet).getByTestId(ids.capacityDays).fill('6');
      await page.getByTestId(touchTestIds.sheetClose).tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
      await expect(page.getByTestId(ids.capacityTotal)).not.toHaveText('21.5');
    },
  },
  {
    name: 'capacity-days',
    route: `/p/SHIP/sprints/${SPRINT_24}/capacity`,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.capacityRow).nth(4).tap();
      const sheet = page.getByTestId(touchTestIds.sheet);
      await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Ana Costa');
      await expect(sheet.getByTestId(ids.capacityDays)).toHaveValue('5');
      await expect(sheet.getByTestId(ids.capacityRemove)).toContainText('Remove from Sprint 24');
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
        page.getByTestId(touchTestIds.sheet).getByTestId(touchTestIds.actionSheetRow).nth(1),
      ).toHaveText('ClearRemove everyone; Sprint 24 plans against velocity (21)');
    },
  },
  {
    name: 'capacity-add',
    route: `/p/SHIP/sprints/${SPRINT_24}/capacity`,
    shell: true,
    arrange: async (page) => {
      await arrange(page);
      await page.route(`**/api/sprints/${SPRINT_24}/capacity`, (route) =>
        route.fulfill({
          json: {
            ...capacitySprint24,
            members: capacitySprint24.members.filter((m) => m.user.id !== PEOPLE.mei.id),
          },
        }),
      );
    },
    ready: async (page) => {
      await expect(page.getByTestId(ids.capacityRow)).toHaveCount(4);
    },
    act: async (page) => {
      await page.getByTestId(ids.capacityAddMember).tap();
      await expect(
        page.getByTestId(touchTestIds.sheet).getByTestId(touchTestIds.actionSheetRow),
      ).toHaveText('MLMei LinStarts at the default, 10 days');
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
      await expect(page.getByTestId(ids.capacityEmpty)).toContainText('No capacity entered');
      await expect(page.getByTestId(ids.capacityAddMember)).toHaveText('+ Add member');
    },
  },
]);
