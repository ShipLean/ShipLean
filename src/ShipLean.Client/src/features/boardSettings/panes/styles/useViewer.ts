import { useQuery } from '@tanstack/react-query';
import { api } from '../../../../api/client';
import type { MeDto } from '../../../../api/me';
import type { Viewer } from './styleSamples';

/** The signed-in user, as the sample cards need them (the shared `['me']` read). */
export function useViewer(): Viewer | undefined {
  const me = useQuery({ queryKey: ['me'], queryFn: () => api.get<MeDto>('/api/me') });
  if (me.data === undefined) return undefined;
  const { id, name, initials, avatarColour, photoUrl } = me.data;
  return { id, name, initials, avatarColour, photoUrl };
}
