import { expect } from '@playwright/experimental-ct-react';
import { retroSharedTestIds as shared } from '../../features/retroShared/testIds';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// The sprint Retro tab on a phone (SHIP-727): the mockups sprint-retro, -unplanned, -decide, -items, -action and
// -coverage in design/mobile/images, from the pinned fixtures.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
}

const ready = async (page: Page) => {
  await expect(page.getByTestId(ids.sprintRetroPanel)).toBeVisible();
  await expect(page.getByTestId(ids.retroNumbers)).toBeVisible();
  await expect(page.getByTestId(shared.retroNote).first()).toBeVisible();
};

const route = '/p/SHIP/sprints/sprint-24/retro';

phonePageShots([
  { name: 'sprint-retro', route, shell: true, arrange, ready },
  { name: 'sprint-retro-unplanned', route: `${route}?cat=notInPlan`, shell: true, arrange, ready },
  {
    name: 'sprint-retro-decide',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retroNumberToDecide).tap();
      await expect(page.getByTestId(ids.retroNumberToDecide)).toHaveAttribute(
        'aria-pressed',
        'true',
      );
      await expect(page.getByTestId(shared.retroBoardColumn)).toHaveAttribute(
        'data-category',
        'notInPlan',
      );
    },
  },
  {
    name: 'sprint-retro-items',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retroViewByItem).tap();
      await expect(page.getByTestId(ids.retroCardList)).toBeVisible();
      await expect(page.getByTestId(shared.retroCard).first()).toBeVisible();
    },
  },
  {
    name: 'sprint-retro-action',
    route: `${route}?cat=wentBadly`,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(shared.retroNoteAddAction).first().tap();
      await expect(page.getByTestId(shared.retroActionDialog)).toBeVisible();
      await expect(page.getByTestId(shared.retroActionTitle)).toBeVisible();
    },
  },
  {
    name: 'sprint-retro-coverage',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retroNumberRetros).tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
      await expect(page.getByTestId(ids.retroCoverageRow).first()).toBeVisible();
    },
  },
]);
