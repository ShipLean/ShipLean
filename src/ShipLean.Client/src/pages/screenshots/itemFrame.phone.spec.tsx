import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import type { ConflictDto } from '../../api/workItems';
import { mei, story } from '../../features/workItem/ct/fixtures';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubSide } from '../../features/workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../features/workItem/ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../../features/workItem/testIds';
import { touchTestIds } from '../../touch/testIds';
import {
  attachments,
  comments,
  revisions,
  savedCardStory,
  workItemStates,
} from './fixtures/workItem';
import { PEOPLE, SHELL_MEMBERS, SHELL_SPRINTS, stubShell } from './shellStub';
import type { Page } from './shoot';

// The work item page's frame on a phone (SHIP-714, L714a), as element shots of this task's regions only, so the other
// 714 tasks' merges (fields, tasks, rail) don't move them: the header with the section chips, the Save bar, and the
// conflict banner. The conductor shoots the whole pages at lane close-out. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/itemFrame.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = (key: string) => ({ hooksConfig: { route: `/p/SHIP/items/${key}` } });

async function stubItemPage(page: Page) {
  await stubShell(page);
  await stubApi(page, savedCardStory);
  // The form fixture's states, members and sprints are registered last and win; put the shell's back.
  await page.route('**/api/projects/p1/states', (r) => r.fulfill({ json: workItemStates }));
  await page.route('**/api/projects/p1/members', (r) => r.fulfill({ json: SHELL_MEMBERS }));
  await page.route('**/api/projects/p1/sprints', (r) => r.fulfill({ json: SHELL_SPRINTS }));
  await page.route('**/api/work-items/*/comments', (r) => r.fulfill({ json: comments }));
  await stubSide(page, PEOPLE.priya, attachments, revisions);
}

const theirs = { ...story, revision: 8, priority: 1, points: 3, changedBy: mei };
const conflict: ConflictDto = {
  error: 'Someone else saved this item first.',
  code: 'conflict',
  current: theirs,
  theirChanges: [
    {
      rev: 8,
      changedBy: mei,
      changedAt: '2026-10-21T09:00:00+00:00',
      changedAtLabel: '21 Oct',
      origin: 'user',
      agentName: null,
      webhook: null,
      changes: [
        { field: 'priority', from: 2, to: 1, fromLabel: '2 - High', toLabel: '1 - Critical' },
        { field: 'points', from: 5, to: 3, fromLabel: '5', toLabel: '3' },
      ],
    },
  ],
};

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

for (const theme of THEMES) {
  test(`ItemFramePhone_WhenOpened_ShouldMatchItsScreenshot_item-header-chips_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubItemPage(page);
    await mount(<AppRoutes />, route('SHIP-141'));
    await expect(page.getByTestId(ids.attachment)).toHaveCount(attachments.length);
    await expect(page.getByTestId(ids.sectionChip).last()).toHaveText(`History${revisions.length}`);
    const header = (await page.getByTestId(ids.header).boundingBox())!;
    const chips = (await page.getByTestId(ids.sectionChips).boundingBox())!;

    await expect(page).toHaveScreenshot(`item-header-chips.${theme}.png`, {
      animations: 'disabled',
      clip: { x: 0, y: header.y, width: 390, height: chips.y + chips.height - header.y },
    });
  });

  test(`ItemFramePhone_WhenEdited_ShouldMatchItsScreenshot_item-save-bar_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story);
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
    await screen.getByTestId(ids.points).fill('8');
    await screen.getByTestId(ids.priority).getByRole('button', { name: '1', exact: true }).tap();
    const bar = page.getByTestId(touchTestIds.saveBar);
    await expect(bar.getByTestId(touchTestIds.saveBarMessage)).toHaveText('Story points, Priority');

    await expect(bar).toHaveScreenshot(`item-save-bar.${theme}.png`, { animations: 'disabled' });
  });

  test(`ItemFramePhone_WhenSaveIsStale_ShouldMatchItsScreenshot_item-conflict_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story, () => ({ status: 409, body: conflict }));
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
    await screen.getByTestId(ids.points).fill('13');
    await page.getByTestId(touchTestIds.saveBarSave).tap();
    const banner = page.getByTestId(ids.conflict);
    await expect(banner).toBeInViewport();

    await expect(banner).toHaveScreenshot(`item-conflict.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
