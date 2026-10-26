// The one way the SPA talks to the API. It sends the per-tab client header, throws ApiRequestError for any non-2xx,
// and sends a lost session to the sign-in page.
import { isLoginPath, LOGIN_PATH } from '../loginPath';

// The error body every endpoint returns (C#: ShipLean.Contracts.Errors.ApiError and ApiErrorCodes).
export const apiErrorCodes = [
  'not_found',
  'ambiguous',
  'conflict',
  'out_of_scope',
  'blocked',
  'needs_confirmation',
  'validation',
  'not_configured',
  'unauthorized',
  'forbidden',
  'rate_limited',
  'session_ended',
  'password_change_required',
  'internal_error',
  'slug_taken',
  'link_invalid_url',
  'link_duplicate',
  'not_markdown',
  'markdown_too_large',
] as const;

export type ApiErrorCode = (typeof apiErrorCodes)[number];

export interface ApiError {
  error: string;
  code: ApiErrorCode;
  // The request field at fault; absent when no single field is.
  field?: string;
}

export function isApiError(value: unknown): value is ApiError {
  if (typeof value !== 'object' || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.error === 'string' &&
    typeof candidate.code === 'string' &&
    (candidate.field === undefined || typeof candidate.field === 'string') &&
    (apiErrorCodes as readonly string[]).includes(candidate.code)
  );
}

export class ApiRequestError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code: ApiErrorCode,
    // The whole parsed error body, for codes whose body carries more than { code, error } (a 409's ConflictDto).
    public readonly body?: unknown,
  ) {
    super(message);
  }
}

// Names this tab to the server (C#: ClientHeader.Name), which stamps it on the events a request causes so the tab can
// ignore its own. Once per page load, never stored: a reload is a new client.
export const CLIENT_HEADER = 'X-ShipLean-Client';
// randomUUID exists only in secure contexts (not on a self-hosted install over plain HTTP); getRandomValues always does.
export function newClientId(): string {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}
export const clientId = newClientId();

// The sign-in page this page load is already leaving for. When a session ends, several requests fail together: the
// first answers session_ended and its response deletes the cookie, so the ones sent after it answer a plain 401. Each
// later assign() would replace the navigation already under way (location.pathname only changes once it commits), so
// the first redirect stands, except that a named state replaces a plain one.
let leavingFor: 'plain' | 'state' | undefined;

// A 401 means the session is gone. Not on the sign-in page itself, where it is a wrong password.
function sendToLogin() {
  const { pathname, search } = window.location;
  if (isLoginPath(pathname) || leavingFor !== undefined) return;
  leavingFor = 'plain';
  window.location.assign(`${LOGIN_PATH}?returnUrl=${encodeURIComponent(pathname + search)}`);
}

// The two sign-in states the server names with a code: the session ended (password or stamp changed elsewhere, or the
// account was disabled), or the one-time password must be replaced first. Both open the sign-in page in that state,
// with the path to come back to.
function sendToLoginState(state: 'ended' | 'change') {
  const { pathname, search } = window.location;
  if (isLoginPath(pathname) || leavingFor === 'state') return;
  leavingFor = 'state';
  window.location.assign(
    `${LOGIN_PATH}?state=${state}&returnUrl=${encodeURIComponent(pathname + search)}`,
  );
}

async function errorOf(response: Response): Promise<ApiRequestError> {
  try {
    const body: unknown = await response.json();
    if (isApiError(body)) return new ApiRequestError(body.error, response.status, body.code, body);
  } catch {
    // not JSON: handled below
  }
  return new ApiRequestError(
    `Request failed (${response.status})`,
    response.status,
    'internal_error',
  );
}

// Every non-2xx throws an ApiRequestError, and a lost session goes to sign-in; JSON and multipart requests share this.
async function refuse(response: Response): Promise<ApiRequestError> {
  const error = await errorOf(response);
  if (error.code === 'session_ended') sendToLoginState('ended');
  else if (error.code === 'password_change_required') sendToLoginState('change');
  else if (response.status === 401) sendToLogin();
  return error;
}

async function readBody<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  if (text === '') return undefined as T;
  try {
    return JSON.parse(text) as T;
  } catch {
    // e.g. the SPA fallback answering a mistyped /api path with index.html
    throw new ApiRequestError(
      'The server sent an unreadable response.',
      response.status,
      'internal_error',
    );
  }
}

export interface RequestOptions {
  signal?: AbortSignal;
}

async function request<T>(
  method: string,
  path: string,
  body: unknown,
  options: RequestOptions = {},
): Promise<T> {
  const headers: Record<string, string> = { [CLIENT_HEADER]: clientId };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const response = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: 'same-origin',
    signal: options.signal,
  });
  if (!response.ok) throw await refuse(response);
  return readBody<T>(response);
}

/**
 * Sends multipart form data; the browser sets the form's content type and boundary. Errors and sign-in redirects are
 * the JSON requests'. The server's request cap answers 413 with no body, so `tooLarge` is the message for that.
 */
export async function sendForm<T>(
  method: string,
  path: string,
  form: FormData,
  tooLarge: string,
): Promise<T> {
  const response = await fetch(path, {
    method,
    headers: { [CLIENT_HEADER]: clientId },
    body: form,
    credentials: 'same-origin',
  });
  if (!response.ok) {
    const error = await refuse(response);
    throw response.status === 413 && error.code === 'internal_error'
      ? new ApiRequestError(tooLarge, 413, 'validation')
      : error;
  }
  return readBody<T>(response);
}

export const api = {
  get: <T>(path: string, options?: RequestOptions) => request<T>('GET', path, undefined, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('POST', path, body, options),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('PUT', path, body, options),
  patch: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('PATCH', path, body, options),
  delete: <T>(path: string, options?: RequestOptions) =>
    request<T>('DELETE', path, undefined, options),
};
