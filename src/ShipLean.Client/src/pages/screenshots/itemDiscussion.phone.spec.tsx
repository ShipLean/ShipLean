import { expect, test } from '@playwright/experimental-ct-react';
import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
import { mei, priya, story } from '../../features/workItem/ct/fixtures';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { comment, stubComments } from '../../features/workItem/ct/stubDetails';
import { WorkItemDetailsHarness } from '../../features/workItem/ct/WorkItemDetailsHarness';
import { workItemTestIds as ids } from '../../features/workItem/testIds';
import type { Page } from './shoot';

// The Tasks and Discussion sections on a phone (SHIP-714, L714c), as element shots of this task's regions only, so the
// other 714 tasks' merges don't move them. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/itemDiscussion.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

const task = (
  number: number,
  title: string,
  state: ChildSummaryDto['state'],
  assignee: ChildSummaryDto['assignee'] = priya,
): ChildSummaryDto => ({
  id: `t${number}`,
  key: `SHIP-${number}`,
  type: { id: 'task', name: 'Task' },
  title,
  state,
  assignee,
  points: null,
});
const active = { id: 's-t-active', name: 'In Progress', category: 'inProgress' } as const;
const done = { id: 's-t-done', name: 'Done', category: 'completed' } as const;
const withTasks = {
  ...story,
  children: [
    task(
      142,
      'Wire the vault client so card tokens are requested before the order is placed',
      done,
    ),
    task(143, 'Show decline reasons', active),
    task(144, 'Saved card picker', active, null),
  ],
};
const comments = [
  comment('c1', priya, 'Which **vault** do we use for the saved cards?'),
  comment('c2', mei, 'The new one, behind the payments gateway.'),
];

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

for (const theme of THEMES) {
  test(`ItemDiscussionPhone_WhenOpened_ShouldMatchItsScreenshot_item-tasks_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, withTasks);
    await stubComments(page, mei, []);
    const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
    await expect(screen.getByTestId(ids.taskRow)).toHaveCount(3);

    await expect(screen.getByTestId(ids.tasks)).toHaveScreenshot(`item-tasks.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ItemDiscussionPhone_WhenOpened_ShouldMatchItsScreenshot_item-discussion_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story);
    await stubComments(page, mei, comments);
    const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
    await expect(screen.getByTestId(ids.comment)).toHaveCount(2);

    await expect(screen.getByTestId(ids.discussion)).toHaveScreenshot(
      `item-discussion.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`ItemDiscussionPhone_WhenMentionTyped_ShouldMatchItsScreenshot_item-mention_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story);
    await stubComments(page, mei, comments);
    const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
    await screen.getByTestId(ids.commentInput).fill('Can you check, @pri');
    await expect(screen.getByTestId(ids.mentionMenu)).toBeVisible();

    await expect(screen.getByTestId(ids.discussion)).toHaveScreenshot(`item-mention.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
