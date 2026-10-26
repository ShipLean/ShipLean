import { ApiRequestError } from '../../../api/client';
import type { InstanceSettingsDto } from '../../../api/projects';

export type SettingsEdit = Partial<InstanceSettingsDto>;

/** A base URL as the server stores it: trimmed, no trailing slash. */
export const trimBaseUrl = (value: string) => value.trim().replace(/\/+$/, '');

export interface BaseUrlCheck {
  /** The address in the field is the one this page was opened on. */
  matchesAddressInUse: boolean;
  /** The address is not https, so sign-in cookies would not be sent securely. */
  insecure: boolean;
}

// The browser's own normal form (case, default port, punycode host); null when the text isn't an address.
function originOf(value: string): string | null {
  try {
    return new URL(trimBaseUrl(value)).origin;
  } catch {
    return null;
  }
}

/** `origin` is the page's own origin (the browser's `location.origin`), read by the caller. */
export function checkBaseUrl(baseUrl: string, origin: string): BaseUrlCheck {
  return {
    matchesAddressInUse: originOf(baseUrl) === origin,
    insecure: !trimBaseUrl(baseUrl).toLowerCase().startsWith('https://'),
  };
}

/** The edit with every field that equals the saved value dropped, so "dirty" means "differs from the server". */
export function pruneEdit(saved: InstanceSettingsDto, edit: SettingsEdit): SettingsEdit {
  const kept: SettingsEdit = {};
  if (edit.name !== undefined && edit.name !== saved.name) kept.name = edit.name;
  if (edit.baseUrl !== undefined && edit.baseUrl !== saved.baseUrl) kept.baseUrl = edit.baseUrl;
  if (edit.projectCreation !== undefined && edit.projectCreation !== saved.projectCreation)
    kept.projectCreation = edit.projectCreation;
  return kept;
}

export const isForbidden = (error: unknown) =>
  error instanceof ApiRequestError && error.status === 403;

export const errorText = (error: unknown) =>
  error instanceof Error ? error.message : 'Something went wrong. Try again.';
