import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type { ProjectRole } from '../../../api/projects';
import type { OneTimePasswordDto, UserStatus } from '../../../api/users';
import { registerInvalidation } from '../../../realtime';

// TS twins of ShipLean.Contracts.Users.Admin.

export interface AdminUserProjectDto {
  id: string;
  key: string;
  name: string;
  role: ProjectRole;
  /** Disabling this user would leave the project without an active admin. */
  soleActiveAdmin: boolean;
}

export interface AdminUserDto {
  id: string;
  name: string;
  email: string;
  initials: string;
  avatarColour: string;
  photoUrl: string | null;
  isInstanceAdmin: boolean;
  status: UserStatus;
  /** A server label; null when they never signed in. */
  lastSignIn: string | null;
  projects: AdminUserProjectDto[];
}

export interface AdminProjectChoiceDto {
  id: string;
  key: string;
  name: string;
}

export interface AdminUsersDto {
  users: AdminUserDto[];
  administeredProjects: AdminProjectChoiceDto[];
}

export interface AddUserRequest {
  email: string;
  name: string;
  isInstanceAdmin: boolean;
  projectIds: string[];
}

export interface AddUserResponse {
  user: AdminUserDto;
  /** Null in proxy mode: the identity proxy owns sign-in. */
  oneTimePassword: OneTimePasswordDto | null;
}

export const instanceUsersKey = ['instanceUsers'] as const;

const base = '/api/admin/users';
const userUrl = (id: string, tail: string) => `${base}/${encodeURIComponent(id)}/${tail}`;

/** The table and the Projects picker in one request. Membership changes in any project refetch it. */
export function useInstanceUsers() {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('projectChanged', () => {
        void queryClient.invalidateQueries({ queryKey: instanceUsersKey });
      }),
    [queryClient],
  );
  return useQuery({
    queryKey: instanceUsersKey,
    queryFn: () => api.get<AdminUsersDto>(base),
  });
}

/** Every write refetches the table; the shared `['projects']` list follows because memberships may change. */
export function useUsersAdminMutations() {
  const queryClient = useQueryClient();
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: instanceUsersKey });
    await queryClient.invalidateQueries({ queryKey: ['projects'] });
  };
  const run = <TVariables, TResult>(call: (variables: TVariables) => Promise<TResult>) => ({
    mutationFn: call,
    onSuccess: refresh,
  });
  return {
    add: useMutation(run((body: AddUserRequest) => api.post<AddUserResponse>(base, body))),
    issuePassword: useMutation(
      run((id: string) => api.post<OneTimePasswordDto>(userUrl(id, 'one-time-password'))),
    ),
    unlock: useMutation(run((id: string) => api.post<void>(userUrl(id, 'unlock')))),
    setDisabled: useMutation(
      run((v: { id: string; disabled: boolean }) =>
        api.put<void>(userUrl(v.id, 'disabled'), { disabled: v.disabled }),
      ),
    ),
    setInstanceAdmin: useMutation(
      run((v: { id: string; isInstanceAdmin: boolean }) =>
        api.put<void>(userUrl(v.id, 'instance-admin'), { isInstanceAdmin: v.isInstanceAdmin }),
      ),
    ),
    changeEmail: useMutation(
      run((v: { id: string; email: string }) =>
        api.put<void>(userUrl(v.id, 'email'), { email: v.email }),
      ),
    ),
  };
}
