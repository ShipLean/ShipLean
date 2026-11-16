import { expect, test } from '@playwright/experimental-ct-react';
import { SessionEnd } from './SessionEnd';
import {
  agentError,
  completed,
  deliveryFailedAsAdmin,
  deliveryFailedAsMember,
  endedSession,
  runLog,
} from './ct/fixtures';
import { agentPanelTestIds as ids } from './testIds';

test('SessionEnd_WhenDeliveryFailedAsAdmin_ShouldShowTheTextAndTheDetail', async ({
  mount,
  page,
}) => {
  await mount(<SessionEnd session={deliveryFailedAsAdmin} />);

  await expect(page.getByTestId(ids.sessionEnd)).toContainText('Why it ended');
  await expect(page.getByTestId(ids.sessionEnd)).toContainText(deliveryFailedAsAdmin.endText!);
  await expect(page.getByTestId(ids.sessionEnd)).toContainText(deliveryFailedAsAdmin.endDetail!);
  await expect(page.getByTestId(ids.sessionEndRunLog)).toHaveCount(0);
});

test('SessionEnd_WhenDeliveryFailedAsAdmin_ShouldShowTheDetailOnce', async ({ mount, page }) => {
  await mount(<SessionEnd session={deliveryFailedAsAdmin} />);

  const text = (await page.getByTestId(ids.sessionEnd).textContent()) ?? '';
  expect(text.split(deliveryFailedAsAdmin.endDetail!)).toHaveLength(2);
});

test('SessionEnd_WhenAgentErrorHasAMultiLineDetail_ShouldShowTheFullDetail', async ({
  mount,
  page,
}) => {
  await mount(<SessionEnd session={agentError} />);

  // endText carries only the first line, so the whole detail is still shown below it.
  const end = page.getByTestId(ids.sessionEnd);
  await expect(end).toContainText('exit code 1');
  const text = (await end.textContent()) ?? '';
  expect(text).toContain('The build failed.\nexit code 1');
});

test('SessionEnd_WhenDeliveryFailedAsMember_ShouldShowTheTextOnly', async ({ mount, page }) => {
  await mount(<SessionEnd session={deliveryFailedAsMember} />);

  const end = page.getByTestId(ids.sessionEnd);
  await expect(end).toContainText(deliveryFailedAsMember.endText!);
  await expect(end).not.toContainText('503');
  await expect(page.getByTestId(ids.sessionEndRunLog)).toHaveCount(0);
});

test('SessionEnd_WhenTheDetailHoldsMarkup_ShouldShowTheTagsAsText', async ({ mount, page }) => {
  const session = { ...agentError, endDetail: 'Broke at <b>x</b>' };
  await mount(<SessionEnd session={session} />);

  await expect(page.getByTestId(ids.sessionEnd)).toContainText('<b>x</b>');
  await expect(page.getByTestId(ids.sessionEnd).locator('b')).toHaveCount(0);
});

test('SessionEnd_WhenARunLogIsSet_ShouldLinkItsUrl', async ({ mount, page }) => {
  await mount(<SessionEnd session={agentError} />);

  const link = page.getByTestId(ids.sessionEndRunLog);
  await expect(link).toHaveAttribute('href', runLog.url);
  await expect(link).toContainText('Run log');
});

test('SessionEnd_WhenTheSessionIsLive_ShouldRenderNothing', async ({ mount, page }) => {
  const live = endedSession('s-live', { state: 'working', live: true, endedLabel: null });
  await mount(<SessionEnd session={live} />);

  await expect(page.getByTestId(ids.sessionEnd)).toHaveCount(0);
});

test('SessionEnd_WhenEndedWithAnEndText_ShouldShowItWithNoDetailOrLink', async ({
  mount,
  page,
}) => {
  await mount(<SessionEnd session={completed} />);

  await expect(page.getByTestId(ids.sessionEnd)).toContainText('The agent finished.');
  await expect(page.getByTestId(ids.sessionEndRunLog)).toHaveCount(0);
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('SessionEnd_WhenTheDetailIs1000Characters_ShouldWrapWithNoHorizontalScroll', async ({
    mount,
    page,
  }) => {
    const session = { ...deliveryFailedAsAdmin, endDetail: 'x'.repeat(1000) };
    await mount(<SessionEnd session={session} />);

    await expect(page.getByTestId(ids.sessionEnd)).toContainText('x'.repeat(1000));
    const overflow = await page.evaluate(() => ({
      scroll: document.documentElement.scrollWidth,
      client: document.documentElement.clientWidth,
    }));
    expect(overflow.scroll).toBeLessThanOrEqual(overflow.client);
  });
});
