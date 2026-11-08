import { expect, test } from '@playwright/experimental-ct-react';
import { backupsTestIds as ids } from '../../../../backups/testIds';
import { BackupsHarness } from './BackupsHarness';
import { backupRuns, backupsConfigured, backupsNotConfigured, stubBackupsApi } from './backupsStub';

const withHealth = (health: string, runs: unknown[], extra: object = {}) => ({
  ...backupsConfigured,
  health,
  runs,
  ...extra,
});

test('shows ok health with last success', async ({ mount, page }) => {
  await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  const health = view.getByTestId(ids.backupsHealth);
  await expect(health).toHaveAttribute('data-health', 'ok');
  await expect(health).toContainText('Healthy');
  await expect(health).toContainText(
    'Last backup 11h ago, took 47s. Next run 2026-10-25 02:00 UTC.',
  );
  await expect(view.getByTestId(ids.backupsRunNow)).toBeEnabled();
});

test('shows failing health with the error', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    initial: withHealth('failing', [backupRuns.failed], { lastSuccessLabel: '22 Oct' }),
  });
  const view = await mount(<BackupsHarness />);
  const health = view.getByTestId(ids.backupsHealth);
  await expect(health).toHaveAttribute('data-health', 'failing');
  await expect(health).toContainText('Last backup failed');
  await expect(health).toContainText('The bucket refused the upload: Access Denied (403).');
  await expect(health).toContainText('22 Oct');
  await expect(view.getByTestId(ids.backupsRunNow)).toBeEnabled();
});

test('shows overdue health', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    initial: withHealth('overdue', [backupsConfigured.runs[0]], { lastSuccessLabel: '2 days ago' }),
  });
  const view = await mount(<BackupsHarness />);
  const health = view.getByTestId(ids.backupsHealth);
  await expect(health).toHaveAttribute('data-health', 'overdue');
  await expect(health).toContainText('Overdue');
  await expect(health).toContainText('A scheduled run did not happen.');
  await expect(health).toContainText('2 days ago');
});

test('disables run now while running', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    initial: withHealth('running', [backupRuns.running, backupRuns.succeeded]),
  });
  const view = await mount(<BackupsHarness />);
  await expect(view.getByTestId(ids.backupsHealth)).toHaveAttribute('data-health', 'running');
  await expect(view.getByTestId(ids.backupsRunNow)).toBeDisabled();
  await expect(view.getByTestId(ids.backupsRunNow)).toHaveAccessibleDescription(
    'One is already running',
  );
});

test('disables run now when not configured', async ({ mount, page }) => {
  await stubBackupsApi(page, { initial: backupsNotConfigured });
  const view = await mount(<BackupsHarness />);
  await expect(view.getByTestId(ids.backupsHealth)).toHaveAttribute('data-health', 'notConfigured');
  await expect(view.getByTestId(ids.backupsRunNow)).toBeDisabled();
  await expect(view.getByTestId(ids.backupsRunNow)).toHaveAccessibleDescription(
    'Save the settings first',
  );
});

test('shows already-running on conflict', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page, {
    rejectRun: { error: 'Another backup is running on another instance.', startsRunning: true },
  });
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsRunNow).click();
  await expect(view.getByTestId(ids.backupsRunNowError)).toHaveText('A backup is already running.');
  await expect(view.getByTestId(ids.backupsHealth)).toHaveAttribute('data-health', 'running');
  await expect(view.getByTestId(ids.backupsRunNow)).toBeDisabled();
  expect(stub.runRequests).toBe(1);
});

test('shows the server message when not configured on run', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    rejectRun: { error: 'X', code: 'not_configured', status: 503 },
  });
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsRunNow).click();
  await expect(view.getByTestId(ids.backupsRunNowError)).toHaveText('X');
});

test('lists runs newest first with pruned greyed', async ({ mount, page }) => {
  const pruned = { ...backupRuns.succeeded, id: 'pruned-1', startedLabel: '20 Oct', pruned: true };
  await stubBackupsApi(page, {
    initial: withHealth('failing', [backupRuns.succeeded, backupRuns.failed, pruned]),
  });
  const view = await mount(<BackupsHarness />);
  const rows = view.getByTestId(ids.backupsRunRow);
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toContainText('12h ago');
  await expect(rows.nth(0)).toContainText('Scheduled');
  await expect(rows.nth(0)).toContainText('Succeeded');
  await expect(rows.nth(0)).toContainText('46.0 MB');
  await expect(rows.nth(0)).toContainText('12 · 3.0 MB');
  await expect(rows.nth(0)).toContainText('47s');
  await expect(rows.nth(0)).toContainText('g2');
  await expect(rows.nth(1)).toContainText('Failed');
  await expect(rows.nth(2)).toContainText('20 Oct');
  await expect(rows.nth(0)).toHaveAttribute('data-pruned', 'false');
  await expect(rows.nth(2)).toHaveAttribute('data-pruned', 'true');
  await expect(rows.nth(2)).toHaveCSS('opacity', '0.5');
  await expect(rows.nth(2)).toContainText('Pruned');
  // The error opens on demand.
  await expect(view.getByTestId(ids.backupsRunError)).toHaveCount(0);
  await rows.nth(1).getByTestId(ids.backupsRunErrorToggle).click();
  await expect(view.getByTestId(ids.backupsRunError)).toHaveText(
    'The bucket refused the upload: Access Denied (403).',
  );
});

test('shows who started a manual run', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    initial: withHealth('running', [backupRuns.running]),
  });
  const view = await mount(<BackupsHarness />);
  const row = view.getByTestId(ids.backupsRunRow);
  await expect(row).toContainText('Manual, Joel Dickson');
  await expect(row).toContainText('Running');
});

test('shows the new run after run now', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  const rows = view.getByTestId(ids.backupsRunRow);
  await expect(rows).toHaveCount(3);
  await view.getByTestId(ids.backupsRunNow).click();
  await expect(rows).toHaveCount(4);
  await expect(rows.first()).toContainText('Running');
  await expect(rows.first()).toContainText('Manual, Joel Dickson');
  await expect(view.getByTestId(ids.backupsHealth)).toHaveAttribute('data-health', 'running');
  await expect(view.getByTestId(ids.backupsRunNow)).toBeDisabled();
  expect(stub.runRequests).toBe(1);
});
