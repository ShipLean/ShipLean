import { expect, test } from '@playwright/experimental-ct-react';
import {
  deliveryFailedAsAdmin,
  deliveryFailedAsMember,
  endedSession,
  twoAttempts,
} from './ct/fixtures';
import { SessionDeliveries } from './SessionDeliveries';
import { agentPanelTestIds as ids } from './testIds';

const attempts = [
  { ...twoAttempts[0], httpStatus: 502, error: 'The receiver answered 502.' },
  { ...twoAttempts[1], httpStatus: 200, error: null, nextAttemptAt: null, nextAttemptLabel: null },
];

test('SessionDeliveries_WhenTwoAttempts_ShouldListThemOldestFirstCollapsedUntilOpened', async ({
  mount,
  page,
}) => {
  await mount(
    <SessionDeliveries session={endedSession('s1', { state: 'failed', deliveries: attempts })} />,
  );
  const rows = page.getByTestId(ids.sessionDeliveryRow);

  await expect(page.getByTestId(ids.sessionDeliveries)).toBeVisible();
  await expect(rows.first()).toBeHidden();

  await page.getByText('Delivery (2)').click();

  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText('created');
  await expect(rows.nth(0)).toContainText('attempt 1');
  await expect(rows.nth(0)).toContainText('15 minutes ago');
  await expect(rows.nth(0)).toContainText('HTTP 502 · The receiver answered 502.');
  await expect(rows.nth(0)).toContainText('next try 14 minutes ago');
  await expect(rows.nth(1)).toContainText('attempt 2');
  await expect(rows.nth(1)).toContainText('HTTP 200');
  await expect(rows.nth(1)).not.toContainText('next try');
});

test('SessionDeliveries_WhenNoAttempts_ShouldRenderNothing', async ({ mount, page }) => {
  await mount(<SessionDeliveries session={deliveryFailedAsMember} />);

  await expect(page.getByTestId(ids.sessionDeliveries)).toHaveCount(0);
  await expect(page.getByText('Delivery')).toHaveCount(0);
});

test('SessionDeliveries_WhenErrorHoldsMarkup_ShouldRenderItAsText', async ({ mount, page }) => {
  const markup = '<script>window.__pwned = true</script>';
  await mount(
    <SessionDeliveries
      session={endedSession('s2', {
        deliveries: [{ ...twoAttempts[0], error: markup }],
      })}
    />,
  );
  await page.getByText('Delivery (1)').click();

  await expect(page.getByTestId(ids.sessionDeliveryRow)).toContainText(markup);
  expect(await page.evaluate(() => (window as { __pwned?: boolean }).__pwned)).toBeUndefined();
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('SessionDeliveries_WhenLongErrorOnAPhone_ShouldWrapWithoutSidewaysScroll', async ({
    mount,
    page,
  }) => {
    const long = `The receiver answered 503 from ${'x'.repeat(160)}`;
    await mount(
      <SessionDeliveries
        session={endedSession('s3', {
          deliveries: deliveryFailedAsAdmin.deliveries.map((d) => ({ ...d, error: long })),
        })}
      />,
    );
    await page.getByText('Delivery (2)').click();

    await expect(page.getByTestId(ids.sessionDeliveryRow)).toHaveCount(2);
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBeLessThanOrEqual(0);
  });
});
