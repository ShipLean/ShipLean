import { expect, test } from '@playwright/experimental-ct-react';
import { backupsTestIds as ids } from '../../../../backups/testIds';
import type { BackupSettings } from '../backupsApi';
import { BackupsHarness } from './BackupsHarness';
import { DestinationFormHarness } from './DestinationFormHarness';
import { backupsConfigured, backupsNotConfigured, stubBackupsApi } from './backupsStub';

const configured = backupsConfigured as unknown as BackupSettings;
const notConfigured = backupsNotConfigured as unknown as BackupSettings;
const validId = 'destination-harness-valid';
const draftId = 'destination-harness-draft';

test('switches between region and custom endpoint', async ({ mount }) => {
  const view = await mount(<DestinationFormHarness settings={configured} />);
  await expect(view.getByTestId(ids.backupsRegionSelect)).toHaveValue('ap-southeast-1');
  await expect(view.getByTestId(ids.backupsEndpointUrl)).toHaveCount(0);
  await view.getByTestId(ids.backupsEndpointMode).selectOption('custom');
  await expect(view.getByTestId(ids.backupsEndpointUrl)).toBeVisible();
  await expect(view.getByTestId(ids.backupsPathStyle)).toBeChecked();
  await expect(view.getByTestId(ids.backupsRegionSelect)).toHaveValue('us-east-1');
  await view.getByTestId(ids.backupsEndpointMode).selectOption('aws');
  await expect(view.getByTestId(ids.backupsEndpointUrl)).toHaveCount(0);
  await expect(view.getByTestId(ids.backupsPathStyle)).toHaveCount(0);
});

test('warns about http endpoints', async ({ mount }) => {
  const view = await mount(<DestinationFormHarness settings={configured} />);
  await view.getByTestId(ids.backupsEndpointMode).selectOption('custom');
  await view.getByTestId(ids.backupsEndpointUrl).fill('https://ceph.example.com');
  await expect(view.getByTestId(ids.backupsHttpWarning)).toHaveCount(0);
  await view.getByTestId(ids.backupsEndpointUrl).fill('http://10.0.0.5:9000');
  await expect(view.getByTestId(ids.backupsHttpWarning)).toHaveText(
    'Keys are sent unencrypted over http',
  );
});

test('keeps the saved secret unless replaced', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  const lastFour = view.getByTestId(`${ids.backupsSecretKey}-last-four`);
  await expect(lastFour).toContainText('x9Qm');
  await expect(view.getByTestId(ids.backupsSecretKey)).toHaveCount(0);
  await view.getByTestId(ids.backupsBucket).fill('other-bucket');
  await view.getByTestId(ids.backupsSaveButton).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  const kept = stub.puts[0] as { destination: { s3: { secretKey: string | null } } };
  expect(kept.destination.s3.secretKey).toBeNull();
  await view.getByTestId(`${ids.backupsSecretKey}-replace`).click();
  await view.getByTestId(ids.backupsSecretKey).fill('brand-new-secret');
  await view.getByTestId(ids.backupsSaveButton).click();
  await expect.poll(() => stub.puts.length).toBe(2);
  const replaced = stub.puts[1] as { destination: { s3: { secretKey: string | null } } };
  expect(replaced.destination.s3.secretKey).toBe('brand-new-secret');
});

test('reports invalid until passphrases match', async ({ mount }) => {
  const view = await mount(<DestinationFormHarness settings={notConfigured} />);
  await view.getByTestId(ids.backupsBucket).fill('shiplean-backups');
  await expect(view.getByTestId(validId)).toHaveText('false');
  await view.getByTestId(ids.backupsPassphrase).fill('a long phrase');
  await view.getByTestId(ids.backupsPassphraseConfirm).fill('a long phras');
  await expect(view.getByTestId(validId)).toHaveText('false');
  await expect(view.getByTestId(ids.backupsFieldError)).toHaveText(
    'The two passphrases do not match.',
  );
  await view.getByTestId(ids.backupsPassphraseConfirm).fill('a long phrase');
  await expect(view.getByTestId(validId)).toHaveText('true');
  await expect(view.getByTestId(ids.backupsFieldError)).toHaveCount(0);
  await expect(view.getByTestId(draftId)).toContainText('"passphrase":"a long phrase"');
});

test('shows the passphrase generation once saved', async ({ mount }) => {
  const view = await mount(<DestinationFormHarness settings={configured} />);
  await expect(view.getByTestId(ids.backupsPassphraseInfo)).toHaveText('Passphrase #2, set 20 Oct');
  await expect(view.getByText('old backups keep the old passphrase', { exact: false })).toHaveCount(
    0,
  );
  await expect(
    view.getByText('backups already in the destination keep the old passphrase'),
  ).toBeVisible();
  await expect(view.getByTestId(validId)).toHaveText('true');
});

test('disables attachments when inside the database backup', async ({ mount }) => {
  const inDb = {
    ...configured,
    attachmentStore: { kind: 'postgres', inDatabaseBackup: true },
  } as BackupSettings;
  const view = await mount(<DestinationFormHarness settings={inDb} />);
  await expect(view.getByTestId(ids.backupsAttachmentsToggle)).toBeDisabled();
  await expect(view.getByTestId(ids.backupsAttachmentsToggle)).toBeChecked();
  await expect(view.getByText('Already inside the database backup')).toBeVisible();
  await expect(view.getByTestId(ids.backupsAttachmentsNote)).toBeVisible();
});

test('shows the failing step from test connection', async ({ mount, page }) => {
  await stubBackupsApi(page);
  let body: unknown;
  await page.route('**/api/instance/backups/test', (route) => {
    body = route.request().postDataJSON();
    return route.fulfill({
      json: { ok: false, step: 'write', message: 'Access Denied (403).' },
    });
  });
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsTestButton).click();
  await expect(view.getByTestId(ids.backupsTestResult)).toHaveText(
    'Write failed: Access Denied (403).',
  );
  expect((body as { s3: { bucket: string; secretKey: string | null } }).s3).toMatchObject({
    bucket: 'shiplean-backups',
    secretKey: null,
  });
  await view.getByTestId(ids.backupsBucket).fill('x');
  await expect(view.getByTestId(ids.backupsTestResult)).toHaveCount(0);
});

test('shows a server field error under the bucket', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    rejectPut: { error: 'The bucket does not exist.', field: 'destination.s3.bucket' },
  });
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsBucket).fill('missing-bucket');
  await view.getByTestId(ids.backupsSaveButton).click();
  await expect(view.getByTestId(ids.backupsFieldError)).toHaveText('The bucket does not exist.');
  await expect(view.getByTestId(ids.backupsBucket)).toHaveAccessibleDescription(
    'The bucket does not exist.',
  );
});

test('shows server field errors for endpoint, region, access key and passphrase', async ({
  mount,
}) => {
  const view = await mount(
    <DestinationFormHarness
      settings={configured}
      errors={{
        'destination.s3.endpointUrl': 'Endpoint is wrong.',
        'destination.s3.region': 'Region is wrong.',
        'destination.s3.accessKeyId': 'Key is wrong.',
        passphrase: 'Passphrase is wrong.',
      }}
    />,
  );
  await view.getByTestId(ids.backupsEndpointMode).selectOption('custom');
  await expect(view.getByTestId(ids.backupsEndpointUrl)).toHaveAccessibleDescription(
    /Endpoint is wrong\./,
  );
  await expect(view.getByTestId(ids.backupsRegionSelect)).toHaveAccessibleDescription(
    /Region is wrong\./,
  );
  await expect(view.getByTestId(ids.backupsAccessKeyId)).toHaveAccessibleDescription(
    'Key is wrong.',
  );
  await expect(view.getByTestId(ids.backupsPassphrase)).toHaveAccessibleDescription(
    'Passphrase is wrong.',
  );
  await expect(view.getByTestId(ids.backupsFieldError)).toHaveCount(4);
});
