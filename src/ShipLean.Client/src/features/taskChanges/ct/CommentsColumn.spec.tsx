import { expect, test } from '@playwright/experimental-ct-react';
import type { CodeCommentDto } from '@shiplean/core/api/taskChanges';
import { CommentsColumn } from '../CommentsColumn';
import { taskChangesTestIds as ids } from '../testIds';
import { CHANGES } from './fixtures';

const base = CHANGES.comments[0];
const thread = (over: Partial<CodeCommentDto>): CodeCommentDto => ({ ...base, ...over });
const openOne = thread({ id: 'open-1' });
const resolvedOne = thread({ id: 'done-1', resolved: true, replies: [] });
const column = (
  comments: CodeCommentDto[],
  props: Partial<Parameters<typeof CommentsColumn>[0]> = {},
) => (
  <CommentsColumn
    data={{ ...CHANGES, comments }}
    onReply={async () => {}}
    onResolve={async () => {}}
    {...props}
  />
);

test('CommentsColumn_WhenOpenTab_ShouldShowOnlyUnresolvedThreads', async ({ mount, page }) => {
  await mount(column([openOne, resolvedOne]));
  await expect(page.getByTestId(ids.thread)).toHaveCount(1);
  await expect(page.getByTestId(ids.commentsTabOpen)).toContainText('Open (1)');
  await page.getByTestId(ids.commentsTabAll).click();
  await expect(page.getByTestId(ids.thread)).toHaveCount(2);
});

test('CommentsColumn_WhenResolveIsPressed_ShouldCallOnResolveWithTrue', async ({ mount, page }) => {
  const calls: [string, boolean][] = [];
  await mount(
    column([openOne], {
      onResolve: async (id, resolved) => {
        calls.push([id, resolved]);
      },
    }),
  );
  await page.getByTestId(ids.resolve).click();
  expect(calls).toEqual([['open-1', true]]);
});

test('CommentsColumn_WhenResolvedThreadReopened_ShouldCallOnResolveWithFalse', async ({
  mount,
  page,
}) => {
  const calls: [string, boolean][] = [];
  await mount(
    column([resolvedOne], {
      onResolve: async (id, resolved) => {
        calls.push([id, resolved]);
      },
    }),
  );
  await page.getByTestId(ids.commentsTabAll).click();
  await page.getByTestId(ids.resolve).click();
  expect(calls).toEqual([['done-1', false]]);
});

test('CommentsColumn_WhenReplySent_ShouldCallOnReplyWithThreadIdAndBody', async ({
  mount,
  page,
}) => {
  const calls: [string, string][] = [];
  await mount(
    column([openOne], {
      onReply: async (id, body) => {
        calls.push([id, body]);
      },
    }),
  );
  await page.getByTestId(ids.replyOpen).click();
  await page.getByTestId(ids.replyBody).fill('  Fixed.  ');
  await page.getByTestId(ids.replySend).click();
  expect(calls).toEqual([['open-1', 'Fixed.']]);
});

test('CommentsColumn_WhenThreadIsOutdated_ShouldStrikeTheOldLine', async ({ mount, page }) => {
  await mount(column([thread({ outdated: true, sentBack: true })]));
  const old = page.getByTestId(ids.commentOldLine);
  await expect(old).toHaveText('return null;');
  await expect(old).toHaveCSS('text-decoration-line', 'line-through');
  await expect(page.getByTestId(ids.commentSentBack)).toBeVisible();
});

test('CommentsColumn_WhenBodyIsServerHtml_ShouldRenderIt', async ({ mount, page }) => {
  await mount(column([openOne]));
  await expect(page.getByTestId(ids.commentBody).first().locator('p')).toHaveText(
    'This should render the saved cards.',
  );
});
