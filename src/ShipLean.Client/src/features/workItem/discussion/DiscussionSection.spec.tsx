import { expect, test } from '@playwright/experimental-ct-react';
import { mei, priya, story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { comment, stubComments } from '../ct/stubDetails';
import { WorkItemDetailsHarness } from '../ct/WorkItemDetailsHarness';
import { componentTestIds } from '../../../components/testIds';
import { workItemTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const fromPriya = comment('c1', priya, 'Which **vault** do we use?');
const fromMei = comment('c2', mei, 'The new one.');
const byAgent = {
  ...comment('c3', mei, 'Drafted by the agent'),
  agentName: 'Claude Code (claude.ai)',
};

test('DiscussionSection_WhenItemHasComments_ShouldListThemOldestFirstWithServerHtml', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubComments(page, mei, [fromPriya, fromMei, byAgent]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  const rows = screen.getByTestId(ids.comment);
  await expect(rows).toHaveCount(3);
  await expect(screen.getByTestId(ids.commentsCount)).toHaveText('3');
  await expect(rows.nth(0).getByTestId(ids.commentAuthor)).toHaveText('Priya Nair');
  await expect(rows.nth(0).getByTestId(ids.commentWhen)).toHaveText('2 days ago');
  await expect(rows.nth(0).getByTestId(ids.commentBody)).toContainText(
    'Which **vault** do we use?',
  );
  await expect(rows.nth(1).getByTestId(ids.commentBody)).toHaveText('The new one.');
  await expect(rows.nth(2).getByTestId(ids.commentAgent)).toHaveText('via Claude Code (claude.ai)');
  await expect(rows.nth(0).getByTestId(ids.commentAgent)).toHaveCount(0);
});

test('DiscussionSection_WhenNoComments_ShouldSayNoComments', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.commentsEmpty)).toHaveText('No comments yet.');
});

test('DiscussionSection_WhenCommentPosted_ShouldSaveAtOnceWithoutDirtyingTheForm', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromPriya]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.commentInput).fill('Looks good');
  await screen.getByTestId(ids.commentPost).click();

  await expect(screen.getByTestId(ids.comment)).toHaveCount(2);
  await expect(screen.getByTestId(ids.comment).nth(1)).toContainText('Looks good');
  await expect(screen.getByTestId(ids.comment).nth(1).getByTestId(ids.commentWhen)).toHaveText(
    'Just now',
  );
  await expect(screen.getByTestId(ids.commentInput)).toHaveValue('');
  expect(comments.posted).toEqual(['Looks good']);
  await expect(screen.getByTestId(ids.save)).toBeDisabled();
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 7');
  await expect(screen.getByTestId(ids.discard)).toBeHidden();
  expect(api.patches).toEqual([]);
});

test('DiscussionSection_WhenFormIsDirty_ShouldKeepEditsWhenCommentPosted', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  await screen.getByTestId(ids.points).fill('8');

  await screen.getByTestId(ids.commentInput).fill('Estimate raised');
  await screen.getByTestId(ids.commentInput).press('Control+Enter');

  await expect(screen.getByTestId(ids.comment)).toHaveCount(1);
  await expect(screen.getByTestId(ids.points)).toHaveValue('8');
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Unsaved: Story points');
});

test('DiscussionSection_WhenBodyIsBlank_ShouldNotAllowPost', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.commentPost)).toBeDisabled();
  await screen.getByTestId(ids.commentInput).fill('  ');
  await expect(screen.getByTestId(ids.commentPost)).toBeDisabled();
});

test('DiscussionSection_WhenPostFails_ShouldShowTheErrorAndKeepTheText', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, []);
  comments.failNext('That comment is too long.');
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.commentInput).fill('Long one');
  await screen.getByTestId(ids.commentPost).click();

  await expect(screen.getByTestId(ids.commentError)).toHaveText('That comment is too long.');
  await expect(screen.getByTestId(ids.commentInput)).toHaveValue('Long one');
});

test('DiscussionSection_WhenCommentsFailToLoad_ShouldSayItCouldNot', async ({ mount, page }) => {
  await stubApi(page, story);
  await page.route('**/api/work-items/*/comments', (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Boom', code: 'internal_error' }),
    }),
  );
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.commentsLoadFailed)).toBeVisible();
});

test('DiscussionSection_WhenOwnComment_ShouldEditItAndMarkItEdited', async ({ mount, page }) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromPriya, fromMei]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" role="member" />, route);
  const rows = screen.getByTestId(ids.comment);
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0).getByTestId(ids.commentEdit)).toHaveCount(0);

  await rows.nth(1).getByTestId(ids.commentEdit).click();
  await expect(screen.getByTestId(ids.commentEditSave)).toBeDisabled();
  await screen.getByTestId(ids.commentEditInput).fill('The new vault.');
  await screen.getByTestId(ids.commentEditSave).click();

  await expect(rows.nth(1).getByTestId(ids.commentBody)).toHaveText('The new vault.');
  await expect(rows.nth(1).getByTestId(ids.commentEdited)).toHaveText('edited Just now');
  expect(comments.edited).toEqual([{ id: 'c2', body: 'The new vault.' }]);
});

test('DiscussionSection_WhenEditRefused_ShouldShowTheErrorInTheEditor', async ({ mount, page }) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromMei]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" role="member" />, route);

  await screen.getByTestId(ids.commentEdit).click();
  comments.failNext('That comment is too long.');
  await screen.getByTestId(ids.commentEditInput).fill('Far too long');
  await screen.getByTestId(ids.commentEditSave).click();

  await expect(screen.getByTestId(ids.commentError)).toHaveText('That comment is too long.');
  await expect(screen.getByTestId(ids.commentEditInput)).toHaveValue('Far too long');
});

test('DiscussionSection_WhenEditCancelled_ShouldKeepTheComment', async ({ mount, page }) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromMei]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" role="member" />, route);

  await screen.getByTestId(ids.commentEdit).click();
  await screen.getByTestId(ids.commentEditInput).fill('Changed my mind');
  await screen.getByTestId(ids.commentEditCancel).click();

  await expect(screen.getByTestId(ids.commentBody)).toHaveText('The new one.');
  expect(comments.edited).toEqual([]);
});

test('DiscussionSection_WhenMemberArchivesOwnComment_ShouldRemoveItFromTheList', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromPriya, fromMei]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" role="member" />, route);
  const rows = screen.getByTestId(ids.comment);
  await expect(rows.nth(0).getByTestId(ids.commentArchive)).toHaveCount(0);

  await rows.nth(1).getByTestId(ids.commentArchive).click();

  await expect(rows).toHaveCount(1);
  await expect(rows.nth(0).getByTestId(ids.commentAuthor)).toHaveText('Priya Nair');
  expect(comments.archived).toEqual(['c2']);
});

test('DiscussionSection_WhenAdmin_ShouldArchiveAnotherUsersCommentButNotEditIt', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromPriya]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" role="admin" />, route);

  await expect(screen.getByTestId(ids.commentEdit)).toHaveCount(0);
  await screen.getByTestId(ids.commentArchive).click();

  await expect(screen.getByTestId(ids.comment)).toHaveCount(0);
  expect(comments.archived).toEqual(['c1']);
});

test('DiscussionSection_WhenAtTyped_ShouldSuggestMembersByPrefixAndInsertTheName', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const input = screen.getByTestId(ids.commentInput);

  await input.fill('Ask @');
  await expect(screen.getByTestId(ids.mentionOption)).toHaveCount(2);

  await input.fill('Ask @pr');
  const options = screen.getByTestId(ids.mentionOption);
  await expect(options).toHaveCount(1);
  await expect(options).toContainText('Priya Nair');
  await input.press('Enter');

  await expect(input).toHaveValue('Ask @Priya Nair ');
  await expect(screen.getByTestId(ids.mentionMenu)).toHaveCount(0);
  await expect(screen.getByTestId(ids.comment)).toHaveCount(0);
});

test('DiscussionSection_WhenSuggestionClicked_ShouldInsertThatMember', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const input = screen.getByTestId(ids.commentInput);

  await input.fill('@');
  await screen.getByTestId(ids.mentionOption).filter({ hasText: 'Mei Lin' }).click();

  await expect(input).toHaveValue('@Mei Lin ');
});

test('DiscussionSection_WhenArrowDownAndTab_ShouldPickTheSecondSuggestion', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const input = screen.getByTestId(ids.commentInput);

  await input.fill('@');
  await input.press('ArrowDown');
  await input.press('Tab');

  await expect(input).toHaveValue('@Mei Lin ');
});

test('DiscussionSection_WhenEscapePressed_ShouldCloseSuggestions', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const input = screen.getByTestId(ids.commentInput);

  await input.fill('@m');
  await expect(screen.getByTestId(ids.mentionMenu)).toBeVisible();
  await input.press('Escape');

  await expect(screen.getByTestId(ids.mentionMenu)).toHaveCount(0);
});

test('DiscussionSection_WhenAtIsInsideAWord_ShouldNotSuggest', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.commentInput).fill('mail me@pr');

  await expect(screen.getByTestId(ids.mentionMenu)).toHaveCount(0);
});

test('CommentItem_WhenSuperseded_ShouldStrikeThrough', async ({ mount, page }) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromPriya]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const row = screen.getByTestId(ids.comment);
  await expect(row.getByTestId(ids.commentSuperseded)).toHaveCount(0);

  await row.getByTestId(ids.commentMenu).click();
  await row.getByTestId(ids.commentSupersede).click();

  await expect(row.getByTestId(ids.commentSuperseded)).toHaveText('Superseded');
  await expect(row.getByTestId(ids.commentBody)).toHaveCSS('text-decoration-line', 'line-through');
  await expect(row.getByTestId(ids.commentBody)).toContainText('Which **vault** do we use?');
  expect(comments.superseded).toEqual(['c1']);
});

test('CommentItem_WhenSupersedeFails_ShouldShowErrorToast', async ({ mount, page }) => {
  await stubApi(page, story);
  const comments = await stubComments(page, mei, [fromPriya]);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const row = screen.getByTestId(ids.comment);
  comments.failNext('That comment does not exist.');

  await row.getByTestId(ids.commentMenu).click();
  await row.getByTestId(ids.commentSupersede).click();

  await expect(screen.getByTestId(componentTestIds.toast)).toContainText(
    'That comment does not exist.',
  );
  await expect(row.getByTestId(ids.commentSuperseded)).toHaveCount(0);
});
