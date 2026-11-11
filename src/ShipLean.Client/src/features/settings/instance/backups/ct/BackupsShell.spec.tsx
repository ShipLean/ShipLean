import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../../../AppRoutes';
import { pagesTestIds } from '../../../../pages/testIds';
import { stubProjectApi } from '../../../../../pages/pagesFixtures';
import { settingsTestIds } from '../../../testIds';
import { backupsTestIds as ids } from '../../../../backups/testIds';
import { BackupsHarness } from './BackupsHarness';
import { backupsConfigured, backupsNotConfigured, stubBackupsApi } from './backupsStub';

test('shows the backups tab and three sections', async ({ mount, page }) => {
  await stubProjectApi(page);
  await stubBackupsApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/instance/backups' } });
  const tab = page.getByTestId(pagesTestIds.instanceSettingsTab).filter({ hasText: 'Backups' });
  await expect(tab).toHaveAttribute('href', '/instance/backups');
  await expect(tab).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(pagesTestIds.slotInstanceBackups)).toBeVisible();
  await expect(page.getByTestId(ids.backupsPage)).toBeVisible();
  // One mark per section: status, destination, schedule.
  await expect(page.getByTestId(ids.backupsHealth)).toHaveAttribute('data-health', 'ok');
  await expect(page.getByTestId(ids.backupsBucket)).toHaveValue('shiplean-backups');
  await expect(page.getByTestId(ids.backupsCronInput)).toHaveValue('0 2 * * *');
  await expect(page.getByTestId(ids.backupsEnabledToggle)).toBeChecked();
  await expect(page.getByTestId(ids.backupsSaveButton)).toBeDisabled();
});

test('shows the load error when GET fails', async ({ mount, page }) => {
  await stubBackupsApi(page, { failGet: { error: 'The backup settings could not be read.' } });
  await mount(<BackupsHarness />);
  // The error is the component's own root element, so look from the page.
  await expect(page.getByTestId(settingsTestIds.instanceLoadFailed)).toHaveText(
    'The backup settings could not be read.',
  );
  await expect(page.getByTestId(ids.backupsPage)).toHaveCount(0);
});

test('shows the form error when PUT fails', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page, {
    rejectPut: { error: 'The settings changed while you were editing. Reload and try again.' },
  });
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsEnabledToggle).uncheck();
  await view.getByTestId(ids.backupsSaveButton).click();
  await expect(view.getByTestId(ids.backupsFormError)).toHaveText(
    'The settings changed while you were editing. Reload and try again.',
  );
  expect(stub.puts).toHaveLength(1);
  // The draft survives the failure, and a further edit clears the message.
  await expect(view.getByTestId(ids.backupsEnabledToggle)).not.toBeChecked();
  await expect(view.getByTestId(ids.backupsSaveButton)).toBeEnabled();
  await view.getByTestId(ids.backupsEnabledToggle).check();
  await expect(view.getByTestId(ids.backupsFormError)).toHaveCount(0);
});

test('keeps a field error out of the form error', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    rejectPut: { error: 'The bucket does not exist.', field: 'destination.s3.bucket' },
  });
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsBucket).fill('missing-bucket');
  await view.getByTestId(ids.backupsSaveButton).click();
  // The shell hands it to the section by path; only the section shows it.
  await expect(view.getByTestId(ids.backupsSaveButton)).toBeEnabled();
  await expect(view.getByTestId(ids.backupsFormError)).toHaveCount(0);
});

test('sends the draft as the PUT body on save', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsEnabledToggle).uncheck();
  await view.getByTestId(ids.backupsBucket).fill('other-bucket');
  await view.getByTestId(ids.backupsCronInput).fill('30 3 * * 1');
  await view.getByTestId(ids.backupsSaveButton).click();
  const { destination, ...rest } = {
    enabled: false,
    destination: {
      provider: 's3',
      s3: {
        region: 'ap-southeast-1',
        endpointUrl: null,
        bucket: 'other-bucket',
        prefix: 'shiplean/',
        forcePathStyle: false,
        accessKeyId: 'SHIPLEANEXAMPLEKEY02',
        // Untouched: the saved secret and passphrase stay.
        secretKey: null,
      },
    },
    cron: '30 3 * * 1',
    keepCount: 14,
    includeAttachments: true,
    passphrase: null,
  };
  await expect.poll(() => stub.puts).toEqual([{ ...rest, destination }]);
  // Saved: the answer replaces the draft, so Save is off again.
  await expect(view.getByTestId(ids.backupsSaveButton)).toBeDisabled();
  await expect(view.getByTestId(ids.backupsEnabledToggle)).not.toBeChecked();
});

test('blocks save while a section is invalid', async ({ mount, page }) => {
  await stubBackupsApi(page, { initial: backupsNotConfigured });
  const view = await mount(<BackupsHarness />);
  const save = view.getByTestId(ids.backupsSaveButton);
  await expect(save).toBeDisabled();
  // Changed, but there is no bucket, and no passphrase yet: the destination section is invalid.
  await view.getByTestId(ids.backupsEnabledToggle).check();
  await expect(save).toBeDisabled();
});

test('blocks save while the schedule is blank', async ({ mount, page }) => {
  await stubBackupsApi(page, { initial: backupsConfigured });
  const view = await mount(<BackupsHarness />);
  const save = view.getByTestId(ids.backupsSaveButton);
  await view.getByTestId(ids.backupsCronInput).fill('');
  await expect(save).toBeDisabled();
  await view.getByTestId(ids.backupsCronInput).fill('0 3 * * *');
  await expect(save).toBeEnabled();
});
