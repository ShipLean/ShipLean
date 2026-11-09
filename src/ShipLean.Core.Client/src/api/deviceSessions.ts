// Device sessions: how the phone app signs in (a browser handoff, then a bearer secret) and how a person sees and ends
// them. C#: ShipLean.Contracts.Auth device-session DTOs. Nothing is omitted: absent values are null.
import { api, type RequestOptions } from './client';
import type { MeDto } from './me';

export interface DeviceSessionDto {
  id: string;
  name: string;
  /** `shl_dev_` and the secret's next 8 characters: all a list ever shows of it. */
  tokenPrefix: string;
  createdAt: string;
  /** Server-made short day ("3 Nov"). */
  createdLabel: string;
  lastUsedAt: string;
  /** Server-made relative time ("2h ago"). */
  lastUsedLabel: string;
  expiresAt: string;
  /** The session this request used. */
  current: boolean;
}

export interface CreateSignInCodeRequest {
  /** S256 PKCE challenge: 43 base64url characters. */
  challenge: string;
  /** 1–100 characters after trimming. */
  deviceName: string;
}

export interface SignInCodeDto {
  code: string;
}

export interface ExchangeSignInCodeRequest {
  code: string;
  verifier: string;
}

export interface DeviceSignInDto {
  session: DeviceSessionDto;
  /** `shl_dev_` and 43 base62 characters. Shown once: only its hash is stored. */
  secret: string;
  me: MeDto;
}

export const deviceSessionKeys = {
  list: ['auth', 'device-sessions'] as const,
};

const BASE = '/api/auth/device-sessions';

// Signed in with the cookie (the confirm page); a device bearer is refused.
export const createSignInCode = (req: CreateSignInCodeRequest) =>
  api.post<SignInCodeDto>(`${BASE}/codes`, req);

// Anonymous. Its 400 is a refused sign-in, not a lost session, so it never reaches onUnauthorized.
// `options` adds a signal; signIn: true is always sent.
export const exchangeSignInCode = (req: ExchangeSignInCodeRequest, options?: RequestOptions) =>
  api.post<DeviceSignInDto>(`${BASE}/exchange`, req, { ...options, signIn: true });

export const listDeviceSessions = () => api.get<DeviceSessionDto[]>(BASE);

export const revokeDeviceSession = (id: string) =>
  api.delete<void>(`${BASE}/${encodeURIComponent(id)}`);

// Ends the session this request used (the app's Sign out). The app passes { signIn: true } so a late 401 never reaches
// onUnauthorized after the person has signed in again, and a signal to bound the wait.
export const endCurrentDeviceSession = (options?: RequestOptions) =>
  api.delete<void>(`${BASE}/current`, options);
