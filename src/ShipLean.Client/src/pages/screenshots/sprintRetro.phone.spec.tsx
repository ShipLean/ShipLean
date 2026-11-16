import { expect } from '@playwright/experimental-ct-react';
import { retroSharedTestIds as shared } from '../../features/retroShared/testIds';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { SHELL_SPRINTS, stubShell } from './shellStub';
import { sprintHeaderTestIds as headerIds } from '../../features/sprintHeader/testIds';

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

// Sprint 24 ended and closed (its rollover decided): the header's ⋯ then has no Close sprint.
const closedSprints = SHELL_SPRINTS.map((s) =>
  s.id === 'sprint-24'
    ? {
        ...s,
        status: 'past' as const,
        rolloverStatus: 'decided' as const,
        timeLabel: 'Ended today',
      }
    : s,
);

const sheetOpened = (open: (page: Page) => Promise<void>, title: string) => async (page: Page) => {
  await open(page);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText(title);
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
  {
    name: 'sprint-retro-actions',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retroNumberActions).tap();
      await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Action items');
      await expect(page.getByTestId(ids.retroRailActions)).toBeVisible();
    },
  },
  {
    name: 'sprint-retro-recurring',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retroNumberRecurring).tap();
      await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Recurring themes');
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    },
  },
  {
    name: 'sprint-retro-filter',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retroFilterOpen).tap();
      await expect(page.getByTestId(ids.retroFilterShow)).toBeVisible();
    },
  },
  {
    name: 'sprint-retro-more',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retroToolbar).getByTestId(touchTestIds.moreButton).tap();
      await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(5);
    },
  },
  {
    name: 'sprint-retro-sprint',
    route,
    shell: true,
    arrange: async (page) => {
      await stubShell(page, { sprints: closedSprints });
      await stubRetros(page);
    },
    ready,
    act: async (page) => {
      await page.getByTestId(headerIds.header).getByTestId(touchTestIds.moreButton).tap();
      await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(3);
    },
  },
  {
    name: 'sprint-retro-note',
    route: `${route}?cat=wentBadly`,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(shared.retroNote).getByTestId(touchTestIds.moreButton).first().tap();
      await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(2);
    },
  },
  {
    name: 'sprint-retro-theme',
    route,
    shell: true,
    arrange,
    ready,
    // By category groups notes under their theme heading, so a note's own # theme line shows in By work item.
    act: sheetOpened(async (page) => {
      await page.getByTestId(ids.retroViewByItem).tap();
      await page.getByTestId(ids.retroCardToggle).first().tap();
      const theme = page.getByTestId(shared.retroNoteTheme).first();
      await expect(theme).toBeEnabled();
      await theme.tap();
    }, 'Move to theme'),
  },
  {
    name: 'sprint-retro-why',
    route: `${route}?cat=notInPlan`,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      const note = page.getByTestId(shared.retroNote).first();
      await note.getByTestId(shared.retroNoteNotNeeded).tap();
      await expect(page.getByTestId(shared.retroNoteWhyText)).toBeVisible();
    },
  },
]);
