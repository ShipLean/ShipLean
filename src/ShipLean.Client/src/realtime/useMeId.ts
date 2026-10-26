import { useQuery } from '@tanstack/react-query';
import { fetchMe, meKey } from '../queries/shared';

// The signed-in user's id, from the shared ['me'] query (GET /api/me).
export function useMeId(): string | undefined {
  return useQuery({ queryKey: meKey, queryFn: fetchMe }).data?.id;
}
