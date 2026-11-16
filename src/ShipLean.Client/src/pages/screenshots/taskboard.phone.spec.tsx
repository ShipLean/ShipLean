import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import type { MemberDto } from '@shiplean/core/api/projects';
import { taskboardBoard, stubSettings } from '../../features/taskboardSettings/ct/stubSettings';
import { stubTaskboard } from '../../features/taskboard/ct/stubApi';
import { sprintHeaderTestIds as headerIds } from '../../features/sprintHeader/testIds';
import { taskboardTestIds } from '../../features/taskboard/testIds';
import { componentTestIds } from '../../components/testIds';
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

const chip = (page: Page, name: string) =>
  page
    .getByTestId(touchTestIds.pagerChip)
    .filter({ hasText: new RegExp(`^\\s*${name}(?![A-Za-z])`) });

const openCardActions = async (page: Page) => {
  await page.getByTestId(taskboardTestIds.cardActions).first().tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
};

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
  ...(
    [
      [
        'taskboard-active',
        async (page: Page) => {
          await chip(page, 'Active').tap();
          await expect(chip(page, 'Active')).toHaveAttribute('aria-current', 'true');
          // The smooth scroll has settled once the Active page's header is wholly on screen.
          await expect(
            page
              .getByTestId(taskboardTestIds.phonePageHead)
              .filter({ hasText: /^\s*Active/ })
              .first(),
          ).toBeInViewport({ ratio: 1 });
        },
      ],
      [
        'taskboard-add',
        async (page: Page) => {
          await page.getByTestId(taskboardTestIds.addStory).tap();
          await expect(page.getByTestId(taskboardTestIds.addStoryBox)).toBeVisible();
        },
      ],
      [
        'taskboard-assign',
        async (page: Page) => {
          await openCardActions(page);
          await page.locator('[data-action-id="assignee"]').tap();
          await expect(page.getByTestId(taskboardTestIds.assigneeOption).first()).toBeVisible();
        },
      ],
      [
        'taskboard-move-to-story',
        async (page: Page) => {
          await openCardActions(page);
          await page.locator('[data-action-id="story"]').tap();
          await expect(page.getByTestId(taskboardTestIds.storySearch)).toBeVisible();
        },
      ],
      [
        // Backlog order: the closed lines sit where their rows are, and Unparented is the last lane (TBOARD-1).
        'taskboard-end',
        async (page: Page) => {
          const entries = page
            .getByTestId(touchTestIds.pagerPage)
            .first()
            .locator(
              `[data-testid="${taskboardTestIds.lane}"],[data-testid="${taskboardTestIds.closedLine}"]`,
            );
          await expect(entries.last()).toHaveAttribute('data-row', 'unparented');
          await expect(entries.nth(1)).toHaveAttribute('data-testid', taskboardTestIds.closedLine);
          await entries
            .last()
            .evaluate((el) =>
              el.scrollIntoView({ block: 'end', inline: 'nearest', behavior: 'instant' }),
            );
          await expect(entries.last()).toBeInViewport({ ratio: 0.9 });
        },
      ],
      [
        // The lane card's state opens the K1b StateMenu in a sheet, not a native select (SHIP-1830).
        'taskboard-lane-state',
        async (page: Page) => {
          await page.getByTestId(taskboardTestIds.rowState).first().tap();
          await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
          await expect(page.getByTestId(componentTestIds.stateMenu)).toBeVisible();
        },
      ],
      [
        'taskboard-filter',
        async (page: Page) => {
          await page.getByTestId(taskboardTestIds.filterButton).tap();
          await expect(page.getByTestId(taskboardTestIds.filterSheetRow).first()).toBeVisible();
        },
      ],
      [
        'taskboard-filtered',
        async (page: Page) => {
          await page.getByTestId(taskboardTestIds.filterButton).tap();
          await page.getByTestId(taskboardTestIds.filterSheetRow).nth(1).tap();
          await expect(page.getByTestId(taskboardTestIds.filterButton)).toContainText('Filter1');
          await page.getByTestId(touchTestIds.sheetClose).tap();
          await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
          await expect(page.getByTestId(taskboardTestIds.laneHidden).first()).toBeVisible();
        },
      ],
      [
        'taskboard-more',
        async (page: Page) => {
          await page
            .getByTestId(taskboardTestIds.toolbar)
            .getByTestId(touchTestIds.moreButton)
            .tap();
          await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(3);
        },
      ],
      [
        'taskboard-sprint-picker',
        async (page: Page) => {
          await page.getByTestId(headerIds.picker).tap();
          await expect(page.getByTestId(headerIds.pickerSheetTick)).toBeVisible();
        },
      ],
      [
        'taskboard-sprint-more',
        async (page: Page) => {
          await page.getByTestId(headerIds.header).getByTestId(touchTestIds.moreButton).tap();
          await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(4);
        },
      ],
    ] as const
  ).map(([name, act]) => ({
    name,
    route: ROUTE,
    shell: true,
    arrange: (p: Page) => arrange(p),
    ready,
    act,
  })),
]);

// SHIP-1626 TBOARD-2 (K5): at 390px the second page peeks 30-36px at the right edge.
test('TouchPager_OnTheTaskboardPage_ShouldPeekTheNextPage', async ({ mount, page }) => {
  await arrange(page);
  await mount(<AppRoutes />, { hooksConfig: { route: ROUTE } });
  await ready(page);
  const first = (await page.getByTestId(touchTestIds.pagerPage).nth(0).boundingBox())!;
  const second = (await page.getByTestId(touchTestIds.pagerPage).nth(1).boundingBox())!;
  const track = (await page.getByTestId(touchTestIds.pager).boundingBox())!;
  // The design's page: 340px from x=12, whatever the page's own gutter.
  expect(first.x).toBe(12);
  expect(first.width).toBe(340);
  // K5: the next page peeks 30-36px at 390px, on screen and inside the track (the pager bleeds to the screen edges).
  const peek = Math.min(390, track.x + track.width) - second.x;
  expect(peek).toBeGreaterThanOrEqual(30);
  expect(peek).toBeLessThanOrEqual(36);
  // And nothing clips it: 15px in from the screen edge is the second page itself.
  const secondId = await page
    .getByTestId(touchTestIds.pagerPage)
    .nth(1)
    .getAttribute('data-page-id');
  const hit = await page.evaluate(
    ({ x, y, pageTestId }) =>
      document
        .elementFromPoint(x, y)
        ?.closest(`[data-testid="${pageTestId}"]`)
        ?.getAttribute('data-page-id'),
    { x: 375, y: second.y + 10, pageTestId: touchTestIds.pagerPage },
  );
  expect(hit).toBe(secondId);
  // The bleed reaches the screen edges and no further: the page never scrolls sideways.
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(sideways).toBe(false);
});
