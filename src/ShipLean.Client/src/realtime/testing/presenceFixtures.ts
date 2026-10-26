import type { MemberDto } from '../../api/projects';

// Members for the component tests (nothing here is production code).
export function member(n: number, photoUrl: string | null = null): MemberDto {
  return {
    user: {
      id: `user-${n}`,
      name: `Person ${n}`,
      initials: `P${n}`,
      avatarColour: '#3f7d5a',
      photoUrl,
    },
    email: `person${n}@shiplean.local`,
    role: 'member',
    status: 'active',
  };
}
