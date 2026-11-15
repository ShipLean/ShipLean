import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { mei, priya, story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { comment, stubComments } from '../ct/stubDetails';
import { WorkItemDetailsHarness } from '../ct/WorkItemDetailsHarness';
import { workItemTestIds as ids } from '../testIds';

// Discussion on the CT phone project (390×844, SHIP-714 D8, SHIP-805 rules 4/5/8). The action sheet is portalled to the
// body, so it is found on the page.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

test('CommentItem_WhenMoreTapped_ShouldOpenActionSheet', async ({ mount, page }) => {
  await stubApi(page, story);
  const api = await stubComments(page, mei, [comment('c1', mei, 'Which vault do we use?')]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const row = screen.getByTestId(ids.comment);
  // No popover, no inline Edit or Archive at touch: one ⋯ of at least 32px.
  await expect(row.getByTestId(ids.commentMenu)).toHaveCount(0);
  await expect(row.getByTestId(ids.commentEdit)).toHaveCount(0);
  await expect(row.getByTestId(ids.commentArchive)).toHaveCount(0);
  const more = row.getByTestId(touchTestIds.moreButton);
  expect((await more.boundingBox())!.height).toBeGreaterThanOrEqual(32);

  await more.tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Entry options');
  const rows = sheet.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveText(['Edit', 'Mark superseded', 'Archive']);
  await rows.nth(1).tap();
  await expect(sheet).toHaveCount(0);
  await expect.poll(() => api.superseded).toEqual(['c1']);
});

test('CommentItem_WhenSomeoneElsesComment_ShouldOfferNoEdit', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, [comment('c1', priya, 'Which vault do we use?')]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.comment).getByTestId(touchTestIds.moreButton).tap();

  await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveText([
    'Mark superseded',
    'Archive',
  ]);
});

test('CommentComposer_WhenTouch_ShouldHideKeyboardHints', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const discussion = screen.getByTestId(ids.discussion);

  await expect(discussion.getByText('Ctrl+Enter')).toBeHidden();
  await expect(discussion.getByText('Markdown and @mentions')).toBeHidden();
  const input = (await screen.getByTestId(ids.commentInput).boundingBox())!;
  expect(input.height).toBeGreaterThanOrEqual(90);
  const post = (await screen.getByTestId(ids.commentPost).boundingBox())!;
  expect(post.height).toBeGreaterThanOrEqual(40);
  expect(
    await screen.getByTestId(ids.commentInput).evaluate((el) => getComputedStyle(el).fontSize),
  ).toBe('16px');
});

test('MentionMenu_WhenTouch_ShouldOpenAboveTheBox', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.commentInput).fill('Can you check, @pri');

  const menu = screen.getByTestId(ids.mentionMenu);
  await expect(menu).toBeVisible();
  const list = (await menu.boundingBox())!;
  const input = (await screen.getByTestId(ids.commentInput).boundingBox())!;
  expect(list.y + list.height).toBeLessThanOrEqual(input.y);
  const option = (await menu.getByTestId(ids.mentionOption).boundingBox())!;
  expect(option.height).toBeGreaterThanOrEqual(32);
});
