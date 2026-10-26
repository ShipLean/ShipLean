import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import { fetchPreference, preferenceKey } from '../../../queries/shared';

/**
 * A saved preference whose value is a JSON string (the board's per-user view choices). `value` is null until one is
 * saved. Saving shows the choice at once and puts the old one back if the server refuses; `onSaved` runs after it is stored.
 */
export function useStringPreference(name: string) {
  const queryClient = useQueryClient();
  const key = preferenceKey(name);
  const query = useQuery({
    queryKey: key,
    queryFn: () => fetchPreference(name, null),
  });
  const mutation = useMutation({
    mutationFn: (value: string) =>
      api.put<void>(`/api/me/preferences/${encodeURIComponent(name)}`, value),
    onMutate: async (value) => {
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData(key) ?? null;
      queryClient.setQueryData(key, value);
      return { previous };
    },
    onError: (_error, _value, context) => {
      queryClient.setQueryData(key, context?.previous ?? null);
    },
  });
  return {
    value: typeof query.data === 'string' ? query.data : null,
    save: (value: string, onSaved?: () => void) => mutation.mutate(value, { onSuccess: onSaved }),
  };
}
