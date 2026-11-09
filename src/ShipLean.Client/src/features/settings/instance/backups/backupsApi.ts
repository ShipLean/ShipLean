import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
  type Query,
} from '@tanstack/react-query';
import { api } from '@shiplean/core/api/client';
import type { UserRef } from '@shiplean/core/api/refs';

// The wire shapes of the C-PIN and C-HTTP contracts (SHIP-743): the types here are round-tripped against the pinned
// `backup-*.json` files by backupsApi.test.ts. Every label is the server's; nothing here builds a date.

/** The destination kinds. Only S3 (and S3-compatible stores) for now. */
export type BackupProvider = 's3';
export type BackupHealth = 'ok' | 'running' | 'failing' | 'overdue' | 'neverRun' | 'notConfigured';
export type BackupRunTrigger = 'scheduled' | 'manual';
export type BackupRunState = 'running' | 'succeeded' | 'failed';
export type AttachmentStoreKind = 'localDisk' | 'postgres' | 's3';
export type BackupTestStep = 'write' | 'read' | 'delete';

export interface CronNextRun {
  at: string;
  label: string;
}

/** POST .../cron-preview, and `schedule` of the settings. An invalid expression is `valid: false`, never an error. */
export interface CronPreview {
  valid: boolean;
  error: string | null;
  description: string | null;
  nextRuns: CronNextRun[];
}

export interface BackupRun {
  id: string;
  trigger: BackupRunTrigger;
  state: BackupRunState;
  startedAt: string;
  startedLabel: string;
  finishedAt: string | null;
  durationLabel: string | null;
  startedBy: UserRef | null;
  passphraseGeneration: number;
  databaseObjectKey: string | null;
  databaseBytes: number | null;
  attachmentsCopied: number | null;
  attachmentsBytes: number | null;
  error: string | null;
  pruned: boolean;
}

/** The saved S3 destination; the secret key is never returned, only its last 4 characters. */
export interface BackupS3Saved {
  region: string;
  endpointUrl: string | null;
  bucket: string;
  prefix: string;
  forcePathStyle: boolean;
  accessKeyId: string | null;
  secretKeyLast4: string | null;
}

export interface BackupDestinationSaved {
  provider: BackupProvider;
  s3: BackupS3Saved;
}

export interface BackupRegion {
  code: string;
  name: string;
}

export interface AttachmentStore {
  kind: AttachmentStoreKind;
  /** True when attachments already live in the database that is backed up. */
  inDatabaseBackup: boolean;
}

/** GET and PUT /api/instance/backups. */
export interface BackupSettings {
  enabled: boolean;
  destination: BackupDestinationSaved | null;
  cron: string;
  schedule: CronPreview;
  keepCount: number;
  includeAttachments: boolean;
  attachmentStore: AttachmentStore;
  /** 0 when no passphrase is set. */
  passphraseGeneration: number;
  passphraseSetAt: string | null;
  passphraseSetLabel: string | null;
  health: BackupHealth;
  lastSuccessAt: string | null;
  lastSuccessLabel: string | null;
  /** Newest first, at most 20. */
  runs: BackupRun[];
  regions: BackupRegion[];
  updatedAt: string | null;
  updatedLabel: string | null;
}

/** What goes up for S3. `secretKey`: null keeps the saved one, "" clears it (then `accessKeyId` must be null). */
export interface BackupS3Save {
  region: string;
  endpointUrl: string | null;
  bucket: string;
  prefix: string;
  forcePathStyle: boolean;
  accessKeyId: string | null;
  secretKey: string | null;
}

/** The body of POST .../test, and `destination` of the save body. */
export interface BackupDestinationSave {
  provider: BackupProvider;
  s3: BackupS3Save;
}

/** The PUT body. `passphrase`: null keeps the saved one; it is required when none is saved. */
export interface BackupSave {
  enabled: boolean;
  destination: BackupDestinationSave;
  cron: string;
  keepCount: number;
  includeAttachments: boolean;
  passphrase: string | null;
}

/** POST .../test: a failed probe is `ok: false` with its step and message, never an error response. */
export interface BackupTestResult {
  ok: boolean;
  step: BackupTestStep | null;
  message: string;
}

export const backupsKey = ['instance', 'backups'] as const;

/** How often the settings are re-read while a run is going, in ms. */
export const RUNNING_POLL_MS = 5000;

export const hasRunningRun = (settings: BackupSettings | undefined): boolean =>
  settings?.runs.some((run) => run.state === 'running') ?? false;

/** The settings query's `refetchInterval`: every 5 seconds while any run in the data is running, otherwise never. */
export const backupPollInterval = (settings: BackupSettings | undefined): number | false =>
  hasRunningRun(settings) ? RUNNING_POLL_MS : false;

export function getBackups(): Promise<BackupSettings> {
  return api.get<BackupSettings>('/api/instance/backups');
}

export function putBackups(body: BackupSave): Promise<BackupSettings> {
  return api.put<BackupSettings>('/api/instance/backups', body);
}

export function testBackupDestination(body: BackupDestinationSave): Promise<BackupTestResult> {
  return api.post<BackupTestResult>('/api/instance/backups/test', body);
}

export function previewCron(cron: string): Promise<CronPreview> {
  return api.post<CronPreview>('/api/instance/backups/cron-preview', { cron });
}

export function runBackup(): Promise<BackupRun> {
  return api.post<BackupRun>('/api/instance/backups/run');
}

/** The settings query. It owns the polling, so a section never sets up its own timer. */
export function backupSettingsQuery() {
  return {
    queryKey: backupsKey,
    queryFn: getBackups,
    refetchInterval: (query: Query<BackupSettings>) => backupPollInterval(query.state.data),
  };
}

export function useBackupSettings() {
  return useQuery(backupSettingsQuery());
}

/** Saves the whole tab; the answer replaces the cached settings. */
export function useSaveBackups() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: putBackups,
    onSuccess: (stored) => client.setQueryData(backupsKey, stored),
  });
}

/** Test connection: writes, reads and deletes one small object with the draft's destination. */
export function useTestBackupDestination() {
  return useMutation({ mutationFn: testBackupDestination });
}

/**
 * The server's reading of a cron expression. Pass the (debounced) text; an empty one asks nothing. The previous answer
 * stays while the next loads, so the description does not flicker on every key.
 */
export function useCronPreview(cron: string) {
  const text = cron.trim();
  return useQuery({
    queryKey: [...backupsKey, 'cron-preview', text],
    queryFn: () => previewCron(text),
    enabled: text !== '',
    placeholderData: keepPreviousData,
    staleTime: Infinity,
  });
}

/** Back up now. A started run makes the settings re-read at once, so the history shows it running. */
export function useRunBackupNow() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: runBackup,
    onSuccess: () => client.invalidateQueries({ queryKey: backupsKey, exact: true }),
  });
}
