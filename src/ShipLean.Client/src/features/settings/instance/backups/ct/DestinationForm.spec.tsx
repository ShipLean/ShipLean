import { expect, test } from '@playwright/experimental-ct-react';
import testResultJson from '../../../../../../../ShipLean.TestSupport/PinnedShapes/backup-test-result.json' with { type: 'json' };
import { backupsTestIds as ids } from '../../../../backups/testIds';
import { BackupsHarness } from './BackupsHarness';
import { backupsConfigured, backupsNotConfigured, stubBackupsApi } from './backupsStub';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

// SHIP-796: the destination, retention, encryption and attachments section, mounted through the whole tab so the
// shell's draft, Save and error mapping are the real ones.

/** Answers POST .../test with `result`, and records each body. */
async function stubTest(page: Page, result: unknown): Promise<unknown[]> {
  const bodies: unknown[] = [];
  await page.route('**/api/instance/backups/test', (route) => {
    bodies.push(route.request().postDataJSON());
    return route.fulfill({ json: result });
  });
  return bodies;
}

/** Answers every PUT with a 400 that blames `field()` at the time of the request. Register after stubBackupsApi. */
async function rejectPutsOn(page: Page, field: () => string): Promise<unknown[]> {
  const puts: unknown[] = [];
  await page.route('**/api/instance/backups', (route) => {
    if (route.request().method() !== 'PUT') return route.fallback();
    puts.push(route.request().postDataJSON());
    return route.fulfill({
      status: 400,
      json: { error: `Bad ${field()}.`, code: 'validation', field: field() },
    });
  });
  return puts;
}

test('switches between region and custom endpoint', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  const mode = view.getByTestId(ids.backupsEndpointMode);
  await expect(mode).toHaveValue('aws');
  await expect(view.getByTestId(ids.backupsRegionSelect)).toHaveValue('ap-southeast-1');
  await expect(view.getByTestId(ids.backupsEndpointUrl)).toHaveCount(0);

  await mode.selectOption('custom');
  // A custom endpoint takes a free-text signing region (us-east-1 by default) and path style on.
  await expect(view.getByTestId(ids.backupsRegionSelect)).toHaveValue('us-east-1');
  await expect(view.getByTestId(ids.backupsPathStyle)).toBeChecked();
  await view.getByTestId(ids.backupsEndpointUrl).fill('https://ceph.lab.internal:7480');
  await view.getByTestId(ids.backupsRegionSelect).fill('default');
  await view.getByTestId(ids.backupsSaveButton).click();
  await expect
    .poll(() => (stub.puts[0] as { destination?: unknown } | undefined)?.destination)
    .toEqual({
      provider: 's3',
      s3: {
        region: 'default',
        endpointUrl: 'https://ceph.lab.internal:7480',
        bucket: 'shiplean-backups',
        prefix: 'shiplean/',
        forcePathStyle: true,
        accessKeyId: 'SHIPLEANEXAMPLEKEY02',
        secretKey: null,
      },
    });

  // Back to Amazon: the region is a list again and the endpoint fields are gone.
  await mode.selectOption('custom');
  await mode.selectOption('aws');
  await expect(view.getByTestId(ids.backupsEndpointUrl)).toHaveCount(0);
  await expect(view.getByTestId(ids.backupsPathStyle)).toHaveCount(0);
  await expect(view.getByTestId(ids.backupsRegionSelect)).toHaveValue('us-east-1');
  await view.getByTestId(ids.backupsRegionSelect).selectOption('eu-west-1');
  await expect(view.getByTestId(ids.backupsRegionSelect)).toHaveValue('eu-west-1');
});

test('warns about http endpoints', async ({ mount, page }) => {
  await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsEndpointMode).selectOption('custom');
  const url = view.getByTestId(ids.backupsEndpointUrl);
  await url.fill('https://ceph.lab.internal:7480');
  await expect(view.getByTestId(ids.backupsHttpWarning)).toHaveCount(0);
  await url.fill('http://ceph.lab.internal:7480');
  await expect(view.getByTestId(ids.backupsHttpWarning)).toContainText(
    'Keys are sent unencrypted over http',
  );
  // http is allowed (D10): the warning does not block saving.
  await expect(view.getByTestId(ids.backupsSaveButton)).toBeEnabled();
});

test('keeps the saved secret unless replaced', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  const secret = view.getByTestId(ids.backupsSecretKey);
  // Only the last four characters are ever shown, and the field itself is empty.
  await expect(secret).toHaveValue('');
  await expect(secret).toHaveAttribute('placeholder', 'Saved · ends x9Qm');

  await view.getByTestId(ids.backupsKeepCount).fill('20');
  await view.getByTestId(ids.backupsSaveButton).click();
  const secretOf = (n: number) =>
    (stub.puts[n] as { destination: { s3: { secretKey: string | null } } } | undefined)?.destination
      .s3.secretKey;
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(secretOf(0)).toBeNull();

  await secret.fill('a-new-secret-key');
  await view.getByTestId(ids.backupsSaveButton).click();
  await expect.poll(() => stub.puts.length).toBe(2);
  expect(secretOf(1)).toBe('a-new-secret-key');
});

test('reports invalid until passphrases match', async ({ mount, page }) => {
  await stubBackupsApi(page);
  const view = await mount(<BackupsHarness />);
  const save = view.getByTestId(ids.backupsSaveButton);
  await view.getByTestId(ids.backupsPassphrase).fill('correct horse battery staple');
  await expect(save).toBeDisabled();
  await view.getByTestId(ids.backupsPassphraseConfirm).fill('correct horse battery');
  await expect(save).toBeDisabled();
  await expect(view.getByTestId(ids.backupsPassphraseConfirm)).toHaveAccessibleDescription(
    'The two passphrases do not match.',
  );
  await view.getByTestId(ids.backupsPassphraseConfirm).fill('correct horse battery staple');
  await expect(save).toBeEnabled();
  await expect(view.getByTestId(ids.backupsPassphraseConfirm)).toHaveAccessibleDescription('');
});

test('shows the passphrase generation once saved', async ({ mount, page }) => {
  await stubBackupsApi(page, { initial: backupsNotConfigured });
  const first = await mount(<BackupsHarness />);
  // Nothing saved yet: no generation, and the warning to keep it safe.
  await expect(first.getByTestId(ids.backupsPassphrase)).toBeVisible();
  await expect(first.getByTestId(ids.backupsPassphraseInfo)).toHaveCount(0);
  await expect(first).toContainText("Without this passphrase these backups can't be restored.");
  await first.unmount();

  await stubBackupsApi(page, { initial: backupsConfigured });
  const view = await mount(<BackupsHarness />);
  await expect(view.getByTestId(ids.backupsPassphraseInfo)).toHaveText('Passphrase #2, set 20 Oct');
  await expect(view).toContainText('attachments are copied again');
  await expect(view).toContainText('keep needing passphrase #2');
});

test('disables attachments when inside the database backup', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    initial: {
      ...backupsConfigured,
      attachmentStore: { kind: 'postgres', inDatabaseBackup: true },
    },
  });
  const view = await mount(<BackupsHarness />);
  const toggle = view.getByTestId(ids.backupsAttachmentsToggle);
  await expect(toggle).toBeDisabled();
  await expect(toggle).toHaveAccessibleName('Already inside the database backup');
});

test('hints at a second copy when attachments are in S3', async ({ mount, page }) => {
  await stubBackupsApi(page, {
    initial: { ...backupsConfigured, attachmentStore: { kind: 's3', inDatabaseBackup: false } },
  });
  const view = await mount(<BackupsHarness />);
  await expect(view.getByTestId(ids.backupsAttachmentsToggle)).toBeEnabled();
  await expect(view.getByTestId(ids.backupsAttachmentsNote)).toHaveText(
    'Your attachments are already in S3; turn this on only if you want a second copy.',
  );
});

test('shows the failing step from test connection', async ({ mount, page }) => {
  await stubBackupsApi(page);
  const bodies = await stubTest(page, testResultJson.failed);
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsTestButton).click();
  await expect(view.getByTestId(ids.backupsTestResult)).toHaveText(
    '✕ Write failed: The bucket refused the test object: Access Denied (403).',
  );
  // It tests the draft's destination, with the saved secret kept.
  expect(bodies).toEqual([
    {
      provider: 's3',
      s3: {
        region: 'ap-southeast-1',
        endpointUrl: null,
        bucket: 'shiplean-backups',
        prefix: 'shiplean/',
        forcePathStyle: false,
        accessKeyId: 'SHIPLEANEXAMPLEKEY02',
        secretKey: null,
      },
    },
  ]);
  // A change to the destination clears a result about the old one.
  await view.getByTestId(ids.backupsBucket).fill('other-bucket');
  await expect(view.getByTestId(ids.backupsTestResult)).toHaveCount(0);
});

test('shows ok from test connection', async ({ mount, page }) => {
  await stubBackupsApi(page);
  await stubTest(page, testResultJson.ok);
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsTestButton).click();
  await expect(view.getByTestId(ids.backupsTestResult)).toHaveText(
    '✓ Wrote, read and deleted a test object in shiplean-backups.',
  );
});

test('shows a server field error under the bucket', async ({ mount, page }) => {
  const stub = await stubBackupsApi(page, {
    rejectPut: { error: 'The bucket does not exist.', field: 'destination.s3.bucket' },
  });
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsBucket).fill('missing-bucket');
  await view.getByTestId(ids.backupsSaveButton).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  await expect(view.getByTestId(ids.backupsBucket)).toHaveAccessibleDescription(
    'The bucket does not exist.',
  );
  await expect(view.getByTestId(ids.backupsFieldError)).toHaveText('The bucket does not exist.');
  // After the PUT answered: the field error is not also the form error.
  await expect(view.getByTestId(ids.backupsFormError)).toHaveCount(0);
  // Editing the field clears it.
  await view.getByTestId(ids.backupsBucket).fill('another-bucket');
  await expect(view.getByTestId(ids.backupsFieldError)).toHaveCount(0);
});

test('shows server field errors for endpoint, region, access key and passphrase', async ({
  mount,
  page,
}) => {
  await stubBackupsApi(page);
  let blamed = '';
  const puts = await rejectPutsOn(page, () => blamed);
  const view = await mount(<BackupsHarness />);
  await view.getByTestId(ids.backupsEndpointMode).selectOption('custom');
  await view.getByTestId(ids.backupsEndpointUrl).fill('https://ceph.lab.internal:7480');

  const cases: [string, string][] = [
    ['destination.s3.endpointUrl', ids.backupsEndpointUrl],
    ['destination.s3.region', ids.backupsRegionSelect],
    ['destination.s3.accessKeyId', ids.backupsAccessKeyId],
    ['passphrase', ids.backupsPassphrase],
    ['keepCount', ids.backupsKeepCount],
  ];
  for (const [path, testId] of cases) {
    blamed = path;
    const before = puts.length;
    await view.getByTestId(ids.backupsSaveButton).click();
    await expect.poll(() => puts.length).toBe(before + 1);
    await expect(view.getByTestId(testId)).toHaveAccessibleDescription(`Bad ${path}.`);
    await expect(view.getByTestId(ids.backupsFieldError)).toHaveCount(1);
    await expect(view.getByTestId(ids.backupsFormError)).toHaveCount(0);
  }
});
