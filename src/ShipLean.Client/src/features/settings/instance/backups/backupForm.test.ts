import type { Query } from '@tanstack/react-query';
import { describe, expect, test } from 'vitest';
import { ApiRequestError } from '../../../../api/client';
import runJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-run.json';
import saveJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-save.json';
import settingsJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-settings.json';
import {
  NOTHING_REPORTED,
  allSectionsValid,
  canSave,
  destinationProblems,
  destinationValid,
  draftOf,
  isDirty,
  mapSaveError,
  toDestinationSaveDto,
  toSaveDto,
  withEndpointMode,
  type BackupDraft,
} from './backupForm';
import {
  backupPollInterval,
  backupSettingsQuery,
  hasRunningRun,
  type BackupRun,
  type BackupSettings,
} from './backupsApi';

const configured = settingsJson.configured as BackupSettings;
const notConfigured = settingsJson.notConfigured as BackupSettings;
const running = runJson.running as BackupRun;

const withRuns = (runs: BackupRun[]): BackupSettings => ({ ...configured, runs });

// The `first` example of backup-save.json, typed into an empty form.
const typedFirst = (): BackupDraft => ({
  ...draftOf(notConfigured),
  enabled: true,
  endpointMode: 'custom',
  region: 'us-east-1',
  endpointUrl: 'https://ceph.example.internal:7480',
  forcePathStyle: true,
  bucket: 'shiplean-backups',
  accessKeyId: 'SHIPLEANEXAMPLEKEY01',
  secretKey: 'example-secret-not-a-real-key-7Hq2',
  includeAttachments: true,
  passphrase: 'correct horse battery staple',
  passphraseConfirm: 'correct horse battery staple',
});

describe('toSaveDto', () => {
  test('keeps the saved secret when untouched', () => {
    const draft = draftOf(configured);
    const body = toSaveDto(draft);
    expect(body.destination.s3.secretKey).toBeNull();
    expect(body.destination.s3.accessKeyId).toBe('SHIPLEANEXAMPLEKEY02');
    expect(body).toEqual(saveJson.keepSecrets);
    expect(isDirty(configured, draft)).toBe(false);
  });

  test('clears the secret with an empty string', () => {
    const draft = { ...draftOf(configured), removeKeys: true };
    const s3 = toSaveDto(draft).destination.s3;
    expect(s3.secretKey).toBe('');
    expect(s3.accessKeyId).toBeNull();
    expect(isDirty(configured, draft)).toBe(true);
    expect(destinationValid(draft, configured)).toBe(true);
    // Remove keys wins over a secret typed before it was pressed.
    expect(toSaveDto({ ...draft, secretKey: 'typed' }).destination.s3.secretKey).toBe('');
  });

  test('sends the passphrase only when typed', () => {
    expect(toSaveDto(draftOf(configured)).passphrase).toBeNull();
    const changed = {
      ...draftOf(configured),
      passphrase: 'a new phrase',
      passphraseConfirm: 'a new phrase',
    };
    const body = toSaveDto(changed);
    expect(body.passphrase).toBe('a new phrase');
    // "Type it again" is for the page alone.
    expect(JSON.stringify(body)).not.toContain('passphraseConfirm');
    expect(Object.keys(body).sort()).toEqual(Object.keys(saveJson.first).sort());
    // The first save of a form typed from nothing is the pinned `first` body, field for field.
    expect(toSaveDto(typedFirst())).toEqual(saveJson.first);
  });

  test('sends an Amazon destination without an endpoint or path style', () => {
    const draft = {
      ...draftOf(configured),
      endpointUrl: 'https://left-over',
      forcePathStyle: true,
    };
    const s3 = toDestinationSaveDto(draft).s3;
    expect(s3.endpointUrl).toBeNull();
    expect(s3.forcePathStyle).toBe(false);
  });

  test('trims what was typed and sends blank keys as null', () => {
    const draft = {
      ...typedFirst(),
      bucket: ' b ',
      prefix: ' p/ ',
      accessKeyId: ' ',
      secretKey: '',
    };
    const s3 = toDestinationSaveDto(draft).s3;
    expect(s3.bucket).toBe('b');
    expect(s3.prefix).toBe('p/');
    expect(s3.accessKeyId).toBeNull();
    expect(s3.secretKey).toBeNull();
  });
});

describe('draft', () => {
  test('starts clean from the saved settings and a not-configured one', () => {
    expect(isDirty(configured, draftOf(configured))).toBe(false);
    expect(isDirty(notConfigured, draftOf(notConfigured))).toBe(false);
    const empty = draftOf(notConfigured);
    expect(empty.endpointMode).toBe('aws');
    expect(empty.region).toBe('us-east-1');
    expect(empty.prefix).toBe('shiplean/');
    expect(empty.keepCount).toBe('14');
  });

  test('is dirty once anything is typed', () => {
    const draft = draftOf(configured);
    expect(isDirty(configured, { ...draft, enabled: false })).toBe(true);
    expect(isDirty(configured, { ...draft, secretKey: 'x' })).toBe(true);
    expect(isDirty(configured, { ...draft, passphrase: 'x' })).toBe(true);
    expect(isDirty(configured, { ...draft, keepCount: '15' })).toBe(true);
  });

  test('switching the service gives a custom endpoint the default signing region', () => {
    const custom = withEndpointMode(draftOf(configured), 'custom', configured);
    expect(custom.endpointMode).toBe('custom');
    expect(custom.region).toBe('us-east-1');
    const back = withEndpointMode(
      { ...custom, region: 'my-zone', endpointUrl: 'http://x' },
      'aws',
      configured,
    );
    expect(back.region).toBe('us-east-1');
    expect(back.endpointUrl).toBe('');
    expect(back.forcePathStyle).toBe(false);
    expect(withEndpointMode(back, 'aws', configured)).toBe(back);
  });
});

describe('validity', () => {
  test('blocks save while a section is invalid', () => {
    const clean = draftOf(configured);
    const bothValid = { destination: true, cron: true };
    expect(allSectionsValid(NOTHING_REPORTED)).toBe(false);
    expect(allSectionsValid(bothValid)).toBe(true);
    // Nothing changed: nothing to save.
    expect(canSave({ dirty: false, validity: bothValid, saving: false })).toBe(false);
    expect(canSave({ dirty: true, validity: bothValid, saving: false })).toBe(true);
    // Either section invalid, or nothing reported yet, or a save in flight.
    expect(
      canSave({ dirty: true, validity: { destination: false, cron: true }, saving: false }),
    ).toBe(false);
    expect(
      canSave({ dirty: true, validity: { destination: true, cron: false }, saving: false }),
    ).toBe(false);
    expect(canSave({ dirty: true, validity: NOTHING_REPORTED, saving: false })).toBe(false);
    expect(canSave({ dirty: true, validity: bothValid, saving: true })).toBe(false);
    // The draft rules the destination section reports.
    expect(destinationProblems(clean, configured)).toEqual({});
    expect(destinationValid({ ...clean, bucket: ' ' }, configured)).toBe(false);
    expect(destinationValid({ ...clean, keepCount: '0' }, configured)).toBe(false);
    expect(destinationValid({ ...clean, keepCount: '366' }, configured)).toBe(false);
    expect(destinationValid({ ...clean, keepCount: '1.5' }, configured)).toBe(false);
    expect(destinationValid({ ...clean, keepCount: '365' }, configured)).toBe(true);
  });

  test('needs a passphrase until one is saved, and the same one typed twice', () => {
    const empty = typedFirst();
    expect(destinationProblems(empty, notConfigured)).toEqual({});
    expect(
      destinationProblems({ ...empty, passphrase: '', passphraseConfirm: '' }, notConfigured)
        .passphrase,
    ).toEqual(expect.any(String));
    const mismatch = { ...empty, passphraseConfirm: 'correct horse battery stapl' };
    expect(destinationProblems(mismatch, notConfigured)).toEqual({
      passphraseConfirm: expect.any(String),
    });
    // A saved passphrase can stay as it is; typing a new one needs its copy.
    expect(destinationValid(draftOf(configured), configured)).toBe(true);
    expect(destinationValid({ ...draftOf(configured), passphrase: 'new' }, configured)).toBe(false);
    expect(destinationValid({ ...draftOf(configured), passphraseConfirm: 'new' }, configured)).toBe(
      false,
    );
  });

  test('needs an endpoint address and signing region for a custom endpoint', () => {
    const custom = typedFirst();
    expect(destinationValid(custom, notConfigured)).toBe(true);
    expect(destinationProblems({ ...custom, endpointUrl: '' }, notConfigured).endpointUrl).toEqual(
      expect.any(String),
    );
    expect(destinationValid({ ...custom, endpointUrl: 'ftp://nope' }, notConfigured)).toBe(false);
    expect(
      destinationValid({ ...custom, endpointUrl: 'http://ceph.lab:7480' }, notConfigured),
    ).toBe(true);
    // Free text, not the AWS list.
    expect(destinationValid({ ...custom, region: 'my-ceph-zone' }, notConfigured)).toBe(true);
    expect(destinationProblems({ ...custom, region: ' ' }, notConfigured).region).toEqual(
      expect.any(String),
    );
  });

  test('wants an access key ID and a secret together, or neither', () => {
    const keyless = { ...typedFirst(), accessKeyId: '', secretKey: '' };
    expect(destinationValid(keyless, notConfigured)).toBe(true);
    expect(
      destinationProblems({ ...keyless, accessKeyId: 'AKIA' }, notConfigured).secretKey,
    ).toEqual(expect.any(String));
    expect(
      destinationProblems({ ...keyless, secretKey: 'shh' }, notConfigured).accessKeyId,
    ).toEqual(expect.any(String));
    // A saved secret counts until Remove keys is pressed.
    const saved = draftOf(configured);
    expect(destinationProblems({ ...saved, accessKeyId: '' }, configured).accessKeyId).toEqual(
      expect.any(String),
    );
    expect(destinationValid({ ...saved, accessKeyId: '', removeKeys: true }, configured)).toBe(
      true,
    );
  });
});

describe('polling', () => {
  const queryOf = (settings: BackupSettings | undefined) =>
    ({ state: { data: settings } }) as Query<BackupSettings>;

  test('polls only while a run is running', () => {
    expect(backupPollInterval(withRuns([running]))).toBe(5000);
    expect(backupPollInterval(withRuns([...configured.runs, running]))).toBe(5000);
    expect(backupPollInterval(configured)).toBe(false);
    expect(backupPollInterval(notConfigured)).toBe(false);
    expect(backupPollInterval(undefined)).toBe(false);
    expect(hasRunningRun(withRuns([running]))).toBe(true);
    expect(hasRunningRun(configured)).toBe(false);
    // The hook's query options use exactly this: the interval follows the data in the cache.
    const { refetchInterval } = backupSettingsQuery();
    expect(refetchInterval(queryOf(withRuns([running])))).toBe(5000);
    expect(refetchInterval(queryOf(configured))).toBe(false);
    expect(refetchInterval(queryOf(undefined))).toBe(false);
  });
});

describe('mapSaveError', () => {
  const validation = (field: string | undefined, message = 'No good.') =>
    new ApiRequestError(message, 400, 'validation', { error: message, code: 'validation', field });

  test('passes server field errors to the sections by path', () => {
    for (const path of [
      'destination.s3.bucket',
      'destination.s3.endpointUrl',
      'destination.s3.region',
      'destination.s3.accessKeyId',
      'cron',
      'keepCount',
      'passphrase',
    ]) {
      expect(mapSaveError(validation(path, `Bad ${path}`))).toEqual({
        errors: { [path]: `Bad ${path}` },
        form: undefined,
      });
    }
  });

  test('shows an error with no field, or an unknown one, on the form', () => {
    expect(mapSaveError(validation(undefined, 'Nope.'))).toEqual({ errors: {}, form: 'Nope.' });
    expect(mapSaveError(validation('destination.s3.prefix', 'Odd.'))).toEqual({
      errors: {},
      form: 'Odd.',
    });
    expect(mapSaveError(new ApiRequestError('Forbidden', 403, 'forbidden'))).toEqual({
      errors: {},
      form: 'Forbidden',
    });
    expect(mapSaveError(new TypeError('Failed to fetch'))).toEqual({
      errors: {},
      form: 'Failed to fetch',
    });
    expect(mapSaveError(null)).toEqual({ errors: {}, form: undefined });
  });
});
