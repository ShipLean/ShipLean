import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { ItemAsksDto } from '@shiplean/core/api/asks';
import { RegionHarness } from './ct/RegionHarness';
import { waiting } from './ct/fixtures';
import { stubAsks } from './ct/stubAsks';
import { QuestionList } from './QuestionList';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';

const item: AsksItem = {
  id: waiting.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
};
const view = (asks: ItemAsksDto) => (
  <RegionHarness>
    <QuestionList item={item} asks={asks} />
  </RegionHarness>
);
const row = (page: Page, label: string) =>
  page.getByTestId(ids.questionRow).filter({
    has: page.getByTestId(ids.questionLabel).filter({ hasText: label }),
  });

test('QuestionList_WhenOpenAndAnswered_ShouldListOpenFirst', async ({ mount }) => {
  const asks = structuredClone(waiting);
  const [q1, q2] = asks.questions;
  Object.assign(q1, { state: 'answered', answer: q2.answer });
  Object.assign(q2, { state: 'open', answer: null });
  const component = await mount(view(asks));

  await expect(component.getByTestId(ids.questionLabel)).toHaveText(['Q2', 'Q1']);
  await expect(component.getByTestId(ids.questionRow).first()).toHaveAttribute('id', 'Q2');
  await expect(component.getByTestId(ids.questionState)).toHaveText([
    'Needs an answer',
    '✓ Answered',
  ]);
  await expect(component.getByTestId(ids.questionAnswer)).toContainText('in Discussion');
});

test('QuestionReply_WhenPosted_ShouldCallAnswerAndClose', async ({ mount, page }) => {
  const asks = await stubAsks(page, waiting);
  const component = await mount(view(waiting));

  await row(page, 'Q1').getByTestId(ids.questionReply).click();
  await component.getByTestId(ids.questionReplyInput).fill('  Wait for !57.  ');
  await component.getByTestId(ids.questionReplyPost).click();

  await expect(component.getByTestId(ids.questionReplyInput)).toHaveCount(0);
  expect(asks.writes).toHaveLength(1);
  expect(asks.writes[0].url).toBe(`/api/work-items/${waiting.workItemId}/questions/1/answer`);
  expect(asks.writes[0].body).toEqual({ body: 'Wait for !57.' });
});

test('QuestionReply_WhenServerRefuses_ShouldShowErrorAndKeepText', async ({ mount, page }) => {
  const asks = await stubAsks(page, waiting);
  asks.failNext(409, { error: 'Q1 is no longer open.', code: 'conflict' });
  const component = await mount(view(waiting));

  await row(page, 'Q1').getByTestId(ids.questionReply).click();
  await component.getByTestId(ids.questionReplyInput).fill('Wait for !57.');
  await component.getByTestId(ids.questionReplyPost).click();

  await expect(component.getByTestId(ids.questionReplyError)).toHaveText('Q1 is no longer open.');
  await expect(component.getByTestId(ids.questionReplyInput)).toHaveValue('Wait for !57.');
});

test('QuestionList_WhenWithdrawn_ShouldShowWithdrawn', async ({ mount, page }) => {
  const asks = structuredClone(waiting);
  Object.assign(asks.questions[0], { state: 'withdrawn', withdrawnAtLabel: '5 Nov' });
  await mount(view(asks));

  const withdrawn = row(page, 'Q1');
  await expect(withdrawn.getByTestId(ids.questionState)).toHaveText('Withdrawn');
  await expect(withdrawn.getByTestId(ids.questionReply)).toHaveCount(0);
  await expect(withdrawn.getByTestId(ids.questionMore)).toHaveCount(0);
});

test('QuestionMenu_WhenWithdrawChosen_ShouldPostWithdraw', async ({ mount, page }) => {
  const asks = await stubAsks(page, waiting);
  const component = await mount(view(waiting));

  await row(page, 'Q1').getByTestId(ids.questionMore).click();
  await component.getByTestId(ids.questionWithdraw).click();

  await expect.poll(() => asks.writes.length).toBe(1);
  expect(asks.writes[0].url).toBe(`/api/work-items/${waiting.workItemId}/questions/1/withdraw`);
});

test('QuestionMenu_WhenWithdrawRefused_ShouldShowError', async ({ mount, page }) => {
  const asks = await stubAsks(page, waiting);
  asks.failNext(409, { error: 'Q1 is no longer open.', code: 'conflict' });
  const component = await mount(view(waiting));

  await row(page, 'Q1').getByTestId(ids.questionMore).click();
  await component.getByTestId(ids.questionWithdraw).click();

  await expect(row(page, 'Q1').getByTestId(ids.questionWithdrawError)).toHaveText(
    'Q1 is no longer open.',
  );
});
