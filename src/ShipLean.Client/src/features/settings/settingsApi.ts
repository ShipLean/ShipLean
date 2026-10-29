import { api, sendForm } from '../../api/client';
import type { MeDto } from '../../api/me';
import type { HolidayCountryDto } from '../../api/holidays';
import type { ProjectSettingsDto, ProjectSettingsPut } from '../../api/projects';
import type { UserRef } from '../../api/refs';

/** Keyed under the project like the rest of its data; a save sets it to the document the server returned. */
export const projectSettingsKey = (projectId: string) =>
  ['projects', projectId, 'settings'] as const;

export const fetchProjectSettings = (projectId: string) =>
  api.get<ProjectSettingsDto>(`/api/projects/${encodeURIComponent(projectId)}/settings`);

/** Every country the holiday feed has listed, by name; an empty list when the server has no holiday data. */
export const holidayCountriesKey = ['holidays', 'countries'] as const;

export const fetchHolidayCountries = () => api.get<HolidayCountryDto[]>('/api/holidays/countries');

/** Exact email only: a partial address is a 404 (the server's message says so). */
export const fetchUserByEmail = (email: string) =>
  api.get<UserRef>(`/api/users/by-email?email=${encodeURIComponent(email)}`);

/** One PUT for every section. A refusal (validation, not found, conflict) is thrown as an ApiRequestError. */
export const saveProjectSettings = (projectId: string, put: ProjectSettingsPut) =>
  api.put<ProjectSettingsDto>(`/api/projects/${encodeURIComponent(projectId)}/settings`, put);

/** C#: ShipLean.Contracts.Profile.UpdateProfileRequest. The answer is the caller's MeDto. */
export interface UpdateProfileRequest {
  name: string;
  avatarColour: string;
}

export const saveProfile = (request: UpdateProfileRequest) =>
  api.put<MeDto>('/api/me/profile', request);

/** Multipart upload (the JSON helpers can't send a file). */
export function uploadPhoto(file: File): Promise<MeDto> {
  const form = new FormData();
  form.append('file', file, file.name);
  return sendForm<MeDto>(
    'PUT',
    '/api/me/photo',
    form,
    'That photo is over 2 MB. Pick a smaller one',
  );
}

export const removePhoto = () => api.delete<MeDto>('/api/me/photo');

/** C#: ShipLean.Contracts.Auth.ChangePasswordRequest. The answer is the caller's MeDto; every other session ends. */
export interface ChangePasswordRequest {
  current: string;
  new: string;
}

export const changePassword = (request: ChangePasswordRequest) =>
  api.post<MeDto>('/api/auth/password', request);

export const signOutOthers = () => api.post<void>('/api/auth/sign-out-others');

/** A stored preference whose value is a JSON string (theme, density). */
export const savePreference = (key: string, value: string) =>
  api.put<void>(`/api/me/preferences/${encodeURIComponent(key)}`, value);

export const resetViews = () => api.post<void>('/api/me/preferences/reset-views');

export const leaveProject = (projectId: string) =>
  api.post<void>(`/api/projects/${encodeURIComponent(projectId)}/leave`);
