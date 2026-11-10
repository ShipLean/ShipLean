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

// SHIP-916 W2: an open question's options as quick answers in the Reply composer.
const quick = (page: Page) => page.getByTestId(ids.questionQuickAnswer);
const openComposer = (page: Page) => row(page, 'Q1').getByTestId(ids.questionReply).click();
const optionsOf = (asks: ItemAsksDto) => asks.questions[0].options;

for (const theme of ['light', 'dark'] as const) {
  test(`QuestionReply_WhenQuestionHasOptions_ShouldShowAQuickAnswerPerOption_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const component = await mount(view(waiting));
    await openComposer(page);

    await expect(quick(page)).toHaveText(optionsOf(waiting));
    await expect(component.getByTestId(ids.questionReply).first()).toBeVisible();
    await expect(component.getByTestId(ids.questionReplyInput)).toHaveValue('');
    await expect(row(page, 'Q1')).toHaveScreenshot(`question-quick-answers.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('QuestionReply_WhenQuickAnswerClicked_ShouldFillTheBoxWithoutPosting', async ({
  mount,
  page,
}) => {
  const asks = await stubAsks(page, waiting);
  const component = await mount(view(waiting));
  await openComposer(page);

  await component.getByTestId(ids.questionReplyInput).fill('something typed first');
  await quick(page).nth(1).click();

  const box = component.getByTestId(ids.questionReplyInput);
  await expect(box).toHaveValue(optionsOf(waiting)[1]);
  await expect(box).toBeFocused();
  await expect(component.getByTestId(ids.questionReplyPost)).toBeEnabled();
  expect(asks.writes).toHaveLength(0);
});

test('QuestionReply_WhenQuickAnswerPosted_ShouldSendTheOptionTextAsBody', async ({
  mount,
  page,
}) => {
  const asks = await stubAsks(page, waiting);
  const component = await mount(view(waiting));
  await openComposer(page);

  await quick(page).first().click();
  await component.getByTestId(ids.questionReplyPost).click();

  await expect(component.getByTestId(ids.questionReplyInput)).toHaveCount(0);
  expect(asks.writes).toHaveLength(1);
  expect(asks.writes[0].url).toBe(`/api/work-items/${waiting.workItemId}/questions/1/answer`);
  expect(asks.writes[0].body).toEqual({ body: optionsOf(waiting)[0] });
});

test('QuestionReply_WhenQuickAnswerEdited_ShouldSendTheEditedText', async ({ mount, page }) => {
  const asks = await stubAsks(page, waiting);
  const component = await mount(view(waiting));
  await openComposer(page);

  await quick(page).first().click();
  const box = component.getByTestId(ids.questionReplyInput);
  await box.press('End');
  await box.pressSequentially(', but ask Ana first');
  await component.getByTestId(ids.questionReplyPost).click();

  await expect.poll(() => asks.writes.length).toBe(1);
  expect(asks.writes[0].body).toEqual({ body: `${optionsOf(waiting)[0]}, but ask Ana first` });
});

test('QuestionReply_WhenNoOptions_ShouldShowNoQuickAnswers', async ({ mount, page }) => {
  const asks = structuredClone(waiting);
  asks.questions[0].options = [];
  const component = await mount(view(asks));
  await openComposer(page);

  await expect(component.getByTestId(ids.questionReplyInput)).toBeVisible();
  await expect(quick(page)).toHaveCount(0);
});
