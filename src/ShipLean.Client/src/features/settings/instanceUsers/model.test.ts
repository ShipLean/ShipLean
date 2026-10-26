import { describe, expect, it } from 'vitest';
import { filterUsers, needsAttention, projectsLosingTheirAdmin } from './model';
import type { AdminUserDto } from './usersAdminApi';

const user = (over: Partial<AdminUserDto>): AdminUserDto => ({
  id: 'u',
  name: 'Ann Lee',
  email: 'ann@example.test',
  initials: 'AL',
  avatarColour: '#52627c',
  photoUrl: null,
  isInstanceAdmin: false,
  status: 'active',
  lastSignIn: null,
  projects: [],
  ...over,
});

describe('filterUsers', () => {
  const users = [
    user({ id: '1' }),
    user({ id: '2', name: 'Bo Chan', email: 'bo@example.test', status: 'locked' }),
    user({ id: '3', name: 'Cy Dunn', email: 'cy@example.test', status: 'disabled' }),
  ];

  it('searches name and email, ignoring case', () => {
    expect(filterUsers(users, ' BO@', 'all').map((u) => u.id)).toEqual(['2']);
    expect(filterUsers(users, 'cy d', 'all').map((u) => u.id)).toEqual(['3']);
  });

  it('keeps the disabled out of Active and the settled out of Needs attention', () => {
    expect(filterUsers(users, '', 'active').map((u) => u.id)).toEqual(['1', '2']);
    expect(filterUsers(users, '', 'attention').map((u) => u.id)).toEqual(['2']);
    expect(filterUsers(users, '', 'disabled').map((u) => u.id)).toEqual(['3']);
  });
});

describe('needsAttention', () => {
  it('flags locked, one-time password and no password only', () => {
    expect(
      (['active', 'oneTimePassword', 'locked', 'noPassword', 'disabled'] as const).filter(
        (status) => needsAttention(user({ status })),
      ),
    ).toEqual(['oneTimePassword', 'locked', 'noPassword']);
  });
});

describe('projectsLosingTheirAdmin', () => {
  it('lists only projects where they are the sole active admin', () => {
    const projects = [
      { id: 'a', key: 'AA', name: 'A', role: 'admin' as const, soleActiveAdmin: true },
      { id: 'b', key: 'BB', name: 'B', role: 'admin' as const, soleActiveAdmin: false },
    ];
    expect(projectsLosingTheirAdmin(user({ projects })).map((p) => p.key)).toEqual(['AA']);
  });
});
