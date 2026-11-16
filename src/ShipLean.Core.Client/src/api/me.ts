// The signed-in user. C#: ShipLean.Contracts.Users.MeDto.
import { api, type RequestOptions } from './client';
import type { UserDto } from './users';

export const THEMES = ['os', 'light', 'dark'] as const;
export type Theme = (typeof THEMES)[number];

export const DENSITIES = ['compact', 'comfortable'] as const;
export type Density = (typeof DENSITIES)[number];

export interface MeDto extends UserDto {
  hasProjects: boolean;
  canCreateProjects: boolean;
  theme: Theme;
  density: Density;
  // The user's IANA zone ("Asia/Bangkok"); null when not set, and every label then reads in UTC.
  timeZone: string | null;
  // "Asia/Bangkok (UTC+07:00)", or "Not set: times show in UTC".
  timeZoneLabel: string;
}

// Where a time zone came from: the browser's own zone on first load (applies only while none is set) or the Profile page.
export const TIME_ZONE_SOURCES = ['browser', 'profile'] as const;
export type TimeZoneSource = (typeof TIME_ZONE_SOURCES)[number];

// C#: ShipLean.Contracts.Profile.TimeZoneChoiceDto / TimeZoneChoicesDto.
export interface TimeZoneChoiceDto {
  id: string;
  label: string;
}

export interface TimeZoneChoicesDto {
  choices: TimeZoneChoiceDto[];
}

// GET /api/me, shared by every screen of both clients under one key.
export const meKey = ['me'] as const;
export const getMe = (options?: RequestOptions) => api.get<MeDto>('/api/me', options);

// PUT /api/me/time-zone: answers the MeDto with the zone now stored (a browser value never replaces one already set).
export const setTimeZone = (timeZone: string, source: TimeZoneSource) =>
  api.put<MeDto>('/api/me/time-zone', { timeZone, source });

// GET /api/me/time-zones: every IANA zone the server knows, ordered by offset then id.
export const timeZoneChoices = () => api.get<TimeZoneChoicesDto>('/api/me/time-zones');
