import { expect, test } from 'vitest';
import cronJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-cron-preview.json';
import runJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-run.json';
import saveJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-save.json';
import settingsJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-settings.json';
import testResultJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-test-result.json';
import type {
  BackupRun,
  BackupSave,
  BackupSettings,
  BackupTestResult,
  CronPreview,
} from './backupsApi';

const keysOf = (value: object) => Object.keys(value).sort();
const roundTrip = (value: unknown) => JSON.parse(JSON.stringify(value)) as unknown;

const settingsKeys = [
  'attachmentStore',
  'cron',
  'destination',
  'enabled',
  'health',
  'includeAttachments',
  'keepCount',
  'lastSuccessAt',
  'lastSuccessLabel',
  'passphraseGeneration',
  'passphraseSetAt',
  'passphraseSetLabel',
  'regions',
  'runs',
  'schedule',
  'updatedAt',
  'updatedLabel',
];
const runKeys = [
  'attachmentsBytes',
  'attachmentsCopied',
  'databaseBytes',
  'databaseObjectKey',
  'durationLabel',
  'error',
  'finishedAt',
  'id',
  'passphraseGeneration',
  'pruned',
  'startedAt',
  'startedBy',
  'startedLabel',
  'state',
  'trigger',
];
const cronKeys = ['description', 'error', 'nextRuns', 'valid'];
const healths = ['ok', 'running', 'failing', 'overdue', 'neverRun', 'notConfigured'];

test('BackupsApi_WhenPinnedSettingsRead_ShouldRoundTripThroughTheType', () => {
  for (const [name, example] of Object.entries(settingsJson)) {
    const settings: BackupSettings = example as BackupSettings;
    expect(roundTrip(settings), name).toEqual(example);
    expect(keysOf(settings), name).toEqual(settingsKeys);
    expect(healths, name).toContain(settings.health);
    expect(['localDisk', 'postgres', 's3'], name).toContain(settings.attachmentStore.kind);
    expect(keysOf(settings.attachmentStore), name).toEqual(['inDatabaseBackup', 'kind']);
    expect(keysOf(settings.schedule), name).toEqual(cronKeys);
    expect(settings.regions.length, name).toBeGreaterThan(0);
    for (const region of settings.regions) expect(keysOf(region)).toEqual(['code', 'name']);
    expect(settings.runs.length, name).toBeLessThanOrEqual(20);
  }
});

test('BackupsApi_WhenConfiguredSettingsRead_ShouldKeepSecretsAsLastFourAndRunsTyped', () => {
  const configured: BackupSettings = settingsJson.configured as BackupSettings;
  expect(configured.destination?.provider).toBe('s3');
  const s3 = configured.destination?.s3;
  expect(keysOf(s3 ?? {})).toEqual([
    'accessKeyId',
    'bucket',
    'endpointUrl',
    'forcePathStyle',
    'prefix',
    'region',
    'secretKeyLast4',
  ]);
  expect(s3?.secretKeyLast4).toHaveLength(4);
  expect(s3?.endpointUrl).toBeNull();
  expect(configured.passphraseGeneration).toBe(2);
  expect(configured.runs.map((r) => r.state)).toEqual(['succeeded', 'failed', 'failed']);
  for (const run of configured.runs) expect(keysOf(run)).toEqual(runKeys);
});

test('BackupsApi_WhenNotConfiguredSettingsRead_ShouldHaveNoDestinationAndNullTimestamps', () => {
  const none: BackupSettings = settingsJson.notConfigured as BackupSettings;
  expect(none.enabled).toBe(false);
  expect(none.destination).toBeNull();
  expect(none.health).toBe('notConfigured');
  expect(none.passphraseGeneration).toBe(0);
  expect(none.runs).toEqual([]);
  expect(none.passphraseSetAt).toBeNull();
  expect(none.passphraseSetLabel).toBeNull();
  expect(none.lastSuccessAt).toBeNull();
  expect(none.lastSuccessLabel).toBeNull();
  expect(none.updatedAt).toBeNull();
  expect(none.updatedLabel).toBeNull();
});

test('BackupsApi_WhenPinnedSaveBodiesRead_ShouldRoundTripThroughTheType', () => {
  for (const [name, example] of Object.entries(saveJson)) {
    const save: BackupSave = example as BackupSave;
    expect(roundTrip(save), name).toEqual(example);
    expect(keysOf(save), name).toEqual([
      'cron',
      'destination',
      'enabled',
      'includeAttachments',
      'keepCount',
      'passphrase',
    ]);
    expect(keysOf(save.destination), name).toEqual(['provider', 's3']);
    expect(keysOf(save.destination.s3), name).toEqual([
      'accessKeyId',
      'bucket',
      'endpointUrl',
      'forcePathStyle',
      'prefix',
      'region',
      'secretKey',
    ]);
  }
  const first: BackupSave = saveJson.first as BackupSave;
  expect(first.destination.s3.secretKey).toEqual(expect.any(String));
  expect(first.passphrase).toEqual(expect.any(String));
  const keep: BackupSave = saveJson.keepSecrets as BackupSave;
  expect(keep.destination.s3.secretKey).toBeNull();
  expect(keep.passphrase).toBeNull();
});

test('BackupsApi_WhenPinnedRunsRead_ShouldRoundTripThroughTheType', () => {
  expect(keysOf(runJson)).toEqual(['abandoned', 'failed', 'running', 'succeeded']);
  for (const [name, example] of Object.entries(runJson)) {
    const run: BackupRun = example as BackupRun;
    expect(roundTrip(run), name).toEqual(example);
    expect(keysOf(run), name).toEqual(runKeys);
    expect(['scheduled', 'manual'], name).toContain(run.trigger);
    expect(['running', 'succeeded', 'failed'], name).toContain(run.state);
  }
  const running: BackupRun = runJson.running as BackupRun;
  expect(running.trigger).toBe('manual');
  expect(running.finishedAt).toBeNull();
  expect(running.durationLabel).toBeNull();
  expect(keysOf(running.startedBy ?? {})).toEqual([
    'avatarColour',
    'id',
    'initials',
    'name',
    'photoUrl',
  ]);
  const succeeded: BackupRun = runJson.succeeded as BackupRun;
  expect(succeeded.startedBy).toBeNull();
  expect(succeeded.databaseBytes).toEqual(expect.any(Number));
  expect(succeeded.attachmentsCopied).toEqual(expect.any(Number));
  const failed: BackupRun = runJson.failed as BackupRun;
  expect(failed.error).toEqual(expect.any(String));
  expect(failed.databaseObjectKey).toBeNull();
  const abandoned: BackupRun = runJson.abandoned as BackupRun;
  expect(abandoned.state).toBe('failed');
  expect(abandoned.durationLabel).toBeNull();
});

test('BackupsApi_WhenPinnedCronPreviewsRead_ShouldRoundTripThroughTheType', () => {
  expect(keysOf(cronJson)).toEqual(['invalid', 'tooOften', 'valid']);
  for (const [name, example] of Object.entries(cronJson)) {
    const preview: CronPreview = example as CronPreview;
    expect(roundTrip(preview), name).toEqual(example);
    expect(keysOf(preview), name).toEqual(cronKeys);
    for (const next of preview.nextRuns) expect(keysOf(next), name).toEqual(['at', 'label']);
  }
  const valid: CronPreview = cronJson.valid as CronPreview;
  expect(valid.error).toBeNull();
  expect(valid.nextRuns).toHaveLength(5);
  const invalid: CronPreview = cronJson.invalid as CronPreview;
  expect(invalid.description).toBeNull();
  expect(invalid.nextRuns).toEqual([]);
  const tooOften: CronPreview = cronJson.tooOften as CronPreview;
  expect(tooOften.valid).toBe(false);
  expect(tooOften.description).toEqual(expect.any(String));
  expect(tooOften.nextRuns.length).toBeGreaterThan(0);
});

test('BackupsApi_WhenPinnedTestResultsRead_ShouldRoundTripThroughTheType', () => {
  for (const [name, example] of Object.entries(testResultJson)) {
    const result: BackupTestResult = example as BackupTestResult;
    expect(roundTrip(result), name).toEqual(example);
    expect(keysOf(result), name).toEqual(['message', 'ok', 'step']);
  }
  const ok: BackupTestResult = testResultJson.ok as BackupTestResult;
  expect(ok.step).toBeNull();
  const failed: BackupTestResult = testResultJson.failed as BackupTestResult;
  expect(['write', 'read', 'delete']).toContain(failed.step);
});
