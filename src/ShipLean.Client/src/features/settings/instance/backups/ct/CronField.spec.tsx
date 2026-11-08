import { expect, test } from '@playwright/experimental-ct-react';
import previews from '../../../../../../../ShipLean.TestSupport/PinnedShapes/backup-cron-preview.json' with { type: 'json' };
import { backupsTestIds as ids } from '../../../../backups/testIds';
import { CronFieldHarness } from './CronFieldHarness';

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { postDataJSON(): unknown };
      fulfill(response: { json?: unknown }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

// Answers POST cron-preview by expression: unknown ones are the pinned "invalid" answer.
const stubPreview = async (page: Routable) => {
  const answers: Record<string, unknown> = {
    '0 2 * * *': previews.valid,
    '*/30 * * * *': previews.tooOften,
  };
  await page.route('**/api/instance/backups/cron-preview', (route) => {
    const { cron } = route.request().postDataJSON() as { cron: string };
    return route.fulfill({ json: answers[cron] ?? previews.invalid });
  });
};

test('fills the input from a preset', async ({ mount, page }) => {
  await stubPreview(page);
  const view = await mount(<CronFieldHarness />);
  await view.getByTestId(ids.backupsCronPreset).filter({ hasText: 'Every 6 hours' }).click();
  await expect(view.getByTestId(ids.backupsCronInput)).toHaveValue('0 */6 * * *');
  await view.getByTestId(ids.backupsCronPreset).filter({ hasText: 'Weekly, Sunday 03:00' }).click();
  await expect(view.getByTestId(ids.backupsCronInput)).toHaveValue('0 3 * * 0');
  await view.getByTestId(ids.backupsCronPreset).filter({ hasText: 'Daily at 02:00' }).click();
  await expect(view.getByTestId(ids.backupsCronInput)).toHaveValue('0 2 * * *');
});

test('shows description and five next runs', async ({ mount, page }) => {
  await stubPreview(page);
  const view = await mount(<CronFieldHarness initial="0 2 * * *" />);
  await expect(view.getByTestId(ids.backupsCronDescription)).toHaveText('At 02:00 (UTC)');
  await expect(view.getByTestId(ids.backupsCronNextRun)).toHaveText(
    previews.valid.nextRuns.map((run) => run.label),
  );
  await expect(view.getByTestId(ids.backupsCronError)).toHaveCount(0);
  const describedBy = await view.getByTestId(ids.backupsCronInput).getAttribute('aria-describedby');
  await expect(view.locator(`[id="${describedBy}"]`)).toContainText('At 02:00');
});

test('shows the error for an invalid expression', async ({ mount, page }) => {
  await stubPreview(page);
  const view = await mount(<CronFieldHarness initial="0 2 * *" />);
  await expect(view.getByTestId(ids.backupsCronError)).toHaveText(previews.invalid.error);
  await expect(view.getByTestId(ids.backupsCronNextRun)).toHaveCount(0);
  await expect(view.getByTestId(ids.backupsCronDescription)).toHaveCount(0);
});

test('shows the too-often message', async ({ mount, page }) => {
  await stubPreview(page);
  const view = await mount(<CronFieldHarness initial="*/30 * * * *" />);
  await expect(view.getByTestId(ids.backupsCronError)).toHaveText(
    'Runs must be at least 1 hour apart.',
  );
  await expect(view.getByTestId(ids.backupsCronDescription)).toHaveText('Every 30 minutes (UTC)');
});

test('reports invalid to the form', async ({ mount, page }) => {
  await stubPreview(page);
  const view = await mount(<CronFieldHarness initial="0 2 * * *" />);
  const reported = view.getByTestId('cron-harness-valid');
  await expect(reported).toHaveText('true');
  await view.getByTestId(ids.backupsCronInput).fill('*/30 * * * *');
  await expect(reported).toHaveText('false');
  await view.getByTestId(ids.backupsCronInput).fill('0 2 * * *');
  await expect(reported).toHaveText('true');
});

test('shows the save error from the server', async ({ mount, page }) => {
  await stubPreview(page);
  const view = await mount(<CronFieldHarness initial="0 2 * * *" error="Cron is not accepted." />);
  await expect(view.getByTestId(ids.backupsCronError)).toHaveText('Cron is not accepted.');
});
