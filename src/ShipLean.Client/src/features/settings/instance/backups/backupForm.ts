import { ApiRequestError } from '@shiplean/core/api/client';
import { errorText } from '../model';
import type { BackupDestinationSave, BackupSave, BackupSettings } from './backupsApi';

// The draft of the whole Backups tab, one Save for all of it. The sections edit it through `onChange(patch)`; this file
// is the only place that turns it into the PUT body, decides what is valid and maps a server error onto fields.

export type EndpointMode = 'aws' | 'custom';

/** The sections that report their own validity to the shell. */
export type BackupSection = 'destination' | 'cron';

export interface BackupDraft {
  enabled: boolean;
  /** Amazon S3 (a region from the list) or an S3-compatible endpoint (its own URL and a free-text signing region). */
  endpointMode: EndpointMode;
  region: string;
  endpointUrl: string;
  forcePathStyle: boolean;
  bucket: string;
  prefix: string;
  accessKeyId: string;
  /** What was typed. Empty means untouched: the saved secret is kept. */
  secretKey: string;
  /** "Remove keys" was pressed: save clears the secret and the access key ID. */
  removeKeys: boolean;
  cron: string;
  /** Text, so a half-typed number is not lost; `keepCountOk` says when it is usable. */
  keepCount: string;
  includeAttachments: boolean;
  /** What was typed. Empty means untouched: the saved passphrase is kept. Sent only when typed. */
  passphrase: string;
  /** The "Type it again" field. Client only; it is never sent. */
  passphraseConfirm: string;
}

export const DEFAULT_PREFIX = 'shiplean/';
/** What most S3-compatible stores expect as the signing region. */
export const DEFAULT_CUSTOM_REGION = 'us-east-1';
export const KEEP_COUNT_MIN = 1;
export const KEEP_COUNT_MAX = 365;

/** The draft that matches the saved settings: nothing typed, so nothing is dirty. */
export function draftOf(settings: BackupSettings): BackupDraft {
  const s3 = settings.destination?.s3;
  return {
    enabled: settings.enabled,
    endpointMode: s3?.endpointUrl != null ? 'custom' : 'aws',
    region: s3?.region ?? settings.regions[0]?.code ?? DEFAULT_CUSTOM_REGION,
    endpointUrl: s3?.endpointUrl ?? '',
    forcePathStyle: s3?.forcePathStyle ?? false,
    bucket: s3?.bucket ?? '',
    prefix: s3?.prefix ?? DEFAULT_PREFIX,
    accessKeyId: s3?.accessKeyId ?? '',
    secretKey: '',
    removeKeys: false,
    cron: settings.cron,
    keepCount: String(settings.keepCount),
    includeAttachments: settings.includeAttachments,
    passphrase: '',
    passphraseConfirm: '',
  };
}

/** Switching the service resets what only the other one uses: a custom endpoint gets us-east-1, Amazon a listed region. */
export function withEndpointMode(
  draft: BackupDraft,
  mode: EndpointMode,
  settings: BackupSettings,
): BackupDraft {
  if (mode === draft.endpointMode) return draft;
  if (mode === 'custom') {
    return { ...draft, endpointMode: mode, region: DEFAULT_CUSTOM_REGION, endpointUrl: '' };
  }
  const known = settings.regions.some((r) => r.code === draft.region);
  return {
    ...draft,
    endpointMode: mode,
    region: known ? draft.region : (settings.regions[0]?.code ?? DEFAULT_CUSTOM_REGION),
    endpointUrl: '',
    forcePathStyle: false,
  };
}

const draftKeys = (d: BackupDraft) => Object.keys(d) as (keyof BackupDraft)[];

export function isDirty(settings: BackupSettings, draft: BackupDraft): boolean {
  const saved = draftOf(settings);
  return draftKeys(draft).some((key) => draft[key] !== saved[key]);
}

const orNull = (text: string): string | null => (text.trim() === '' ? null : text.trim());

/**
 * The secret key of a body: "Remove keys" clears it with "", a typed one replaces it, and untouched is null (keep what
 * is saved). Clearing also drops the access key ID, as the contract asks.
 */
function secretKeyOf(draft: BackupDraft): string | null {
  if (draft.removeKeys) return '';
  return draft.secretKey === '' ? null : draft.secretKey;
}

/** The destination as POST .../test and the save body take it. */
export function toDestinationSaveDto(draft: BackupDraft): BackupDestinationSave {
  const custom = draft.endpointMode === 'custom';
  return {
    provider: 's3',
    s3: {
      region: draft.region.trim(),
      endpointUrl: custom ? orNull(draft.endpointUrl) : null,
      bucket: draft.bucket.trim(),
      prefix: draft.prefix.trim(),
      forcePathStyle: custom && draft.forcePathStyle,
      accessKeyId: draft.removeKeys ? null : orNull(draft.accessKeyId),
      secretKey: secretKeyOf(draft),
    },
  };
}

/** The PUT body. The passphrase is sent only when typed; the "Type it again" copy never is. Call it on a valid draft. */
export function toSaveDto(draft: BackupDraft): BackupSave {
  return {
    enabled: draft.enabled,
    destination: toDestinationSaveDto(draft),
    cron: draft.cron.trim(),
    keepCount: Number.parseInt(draft.keepCount, 10),
    includeAttachments: draft.includeAttachments,
    passphrase: draft.passphrase === '' ? null : draft.passphrase,
  };
}

export type DraftField =
  | 'bucket'
  | 'region'
  | 'endpointUrl'
  | 'accessKeyId'
  | 'secretKey'
  | 'keepCount'
  | 'passphrase'
  | 'passphraseConfirm';

export type DraftProblems = Partial<Record<DraftField, string>>;

const isHttpAddress = (text: string): boolean => {
  try {
    const url = new URL(text.trim());
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
};

const keepCountOk = (text: string): boolean => {
  if (!/^\d+$/.test(text.trim())) return false;
  const n = Number.parseInt(text, 10);
  return n >= KEEP_COUNT_MIN && n <= KEEP_COUNT_MAX;
};

/**
 * What stops the destination, retention, encryption and attachments section (one form) from being saved, by field.
 * The server checks everything again; these are the cases the page can name before it asks.
 */
export function destinationProblems(draft: BackupDraft, settings: BackupSettings): DraftProblems {
  const problems: DraftProblems = {};
  const custom = draft.endpointMode === 'custom';
  if (draft.bucket.trim() === '') problems.bucket = 'Enter the bucket name.';
  if (draft.region.trim() === '') {
    problems.region = custom ? 'Enter the signing region.' : 'Choose a region.';
  }
  if (custom && !isHttpAddress(draft.endpointUrl)) {
    problems.endpointUrl = 'Enter an http or https address.';
  }
  const keyGiven = !draft.removeKeys && draft.accessKeyId.trim() !== '';
  const secretKept =
    !draft.removeKeys &&
    (draft.secretKey !== '' || settings.destination?.s3.secretKeyLast4 != null);
  if (keyGiven && !secretKept) problems.secretKey = 'Enter the secret key.';
  if (!keyGiven && secretKept) {
    problems.accessKeyId = 'Enter the access key ID, or remove the keys.';
  }
  if (!keepCountOk(draft.keepCount)) {
    problems.keepCount = `Enter a whole number from ${KEEP_COUNT_MIN} to ${KEEP_COUNT_MAX}.`;
  }
  if (settings.passphraseGeneration === 0 && draft.passphrase === '') {
    problems.passphrase = 'Set a passphrase. Backups can not be opened without it.';
  }
  if (
    (draft.passphrase !== '' || draft.passphraseConfirm !== '') &&
    draft.passphrase !== draft.passphraseConfirm
  ) {
    problems.passphraseConfirm = 'The two passphrases do not match.';
  }
  return problems;
}

export const destinationValid = (draft: BackupDraft, settings: BackupSettings): boolean =>
  Object.keys(destinationProblems(draft, settings)).length === 0;

/** Each section's own validity, as it last reported it. Nothing is valid until a section has said so. */
export type SectionValidity = Record<BackupSection, boolean>;

export const NOTHING_REPORTED: SectionValidity = { destination: false, cron: false };

export const allSectionsValid = (validity: SectionValidity): boolean =>
  Object.values(validity).every(Boolean);

/** Save needs every section valid, something changed, and no save in flight. */
export const canSave = (state: {
  dirty: boolean;
  validity: SectionValidity;
  saving: boolean;
}): boolean => state.dirty && allSectionsValid(state.validity) && !state.saving;

/** The DTO paths a server validation error can name (C-HTTP). */
export const errorPaths = [
  'destination.s3.bucket',
  'destination.s3.endpointUrl',
  'destination.s3.region',
  'destination.s3.accessKeyId',
  'cron',
  'keepCount',
  'passphrase',
] as const;
export type ErrorPath = (typeof errorPaths)[number];

/** DTO path to message, for the sections to show next to their fields. */
export type BackupErrors = Partial<Record<ErrorPath, string>>;

const isErrorPath = (field: unknown): field is ErrorPath =>
  typeof field === 'string' && (errorPaths as readonly string[]).includes(field);

export interface MappedError {
  /** The message for the field the server blames, keyed by its DTO path. */
  errors: BackupErrors;
  /** Everything else: an error with no field (or one no section shows), a failed request. The shell shows it. */
  form: string | undefined;
}

/** Sorts a failed save into field errors for the sections and the one form-level message. */
export function mapSaveError(error: unknown): MappedError {
  if (error === null || error === undefined) return { errors: {}, form: undefined };
  const message = errorText(error);
  if (error instanceof ApiRequestError) {
    const field = (error.body as { field?: unknown } | undefined)?.field;
    if (isErrorPath(field)) return { errors: { [field]: message }, form: undefined };
  }
  return { errors: {}, form: message };
}
