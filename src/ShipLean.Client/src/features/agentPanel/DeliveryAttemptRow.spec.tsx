import { expect, test } from '@playwright/experimental-ct-react';
import { twoAttempts } from './ct/fixtures';
import { DeliveryAttemptRow } from './DeliveryAttemptRow';
import { agentPanelTestIds as ids } from './testIds';

const failed = { ...twoAttempts[0], httpStatus: 502, error: 'The receiver answered 502.' };
const delivered = {
  ...twoAttempts[1],
  httpStatus: 200,
  error: null,
  nextAttemptAt: null,
  nextAttemptLabel: null,
};

test('DeliveryAttemptRow_WhenTheTryFailed_ShouldShowTheErrorInTheAlertColour', async ({
  mount,
  page,
}) => {
  await mount(
    <ol>
      <DeliveryAttemptRow attempt={failed} />
    </ol>,
  );

  const outcome = page.getByTestId(ids.sessionDeliveryOutcome);
  await expect(outcome).toHaveText('HTTP 502 · The receiver answered 502.');
  const alert = await page.evaluate(() => {
    const probe = document.createElement('span');
    probe.className = 'text-alert';
    document.body.append(probe);
    return getComputedStyle(probe).color;
  });
  await expect(outcome).toHaveCSS('color', alert);
});

test('DeliveryAttemptRow_WhenTheTryWasAnswered_ShouldUseTheBodyColourAndNoNextTry', async ({
  mount,
  page,
}) => {
  await mount(
    <ol>
      <DeliveryAttemptRow attempt={delivered} />
    </ol>,
  );

  const outcome = page.getByTestId(ids.sessionDeliveryOutcome);
  await expect(outcome).toHaveText('HTTP 200');
  const body = await page.evaluate(() => getComputedStyle(document.body).color);
  await expect(outcome).toHaveCSS('color', body);
  await expect(page.getByTestId(ids.sessionDeliveryRow)).not.toContainText('next try');
});
