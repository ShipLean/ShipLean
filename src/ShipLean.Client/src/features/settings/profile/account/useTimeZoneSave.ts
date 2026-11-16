import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { setTimeZone } from '@shiplean/core/api/me';
import { meKey } from '../../../../queries/shared';
import { errorText } from './errorText';

/**
 * Saves a time zone picked on the Profile page (source profile, so it replaces any zone already set). On success every
 * query is read again, because each screen's labels are made by the server in the viewer's zone. A refusal keeps the
 * zone as it was and gives the server's words.
 */
export function useTimeZoneSave() {
  const queryClient = useQueryClient();
  const [refusal, setRefusal] = useState<string | null>(null);
  const mutation = useMutation({
    mutationFn: (zone: string) => setTimeZone(zone, 'profile'),
    onMutate: () => setRefusal(null),
    onSuccess: async (me) => {
      queryClient.setQueryData(meKey, me);
      await queryClient.invalidateQueries();
    },
    onError: (error) => setRefusal(errorText(error, 'That time zone could not be saved.')),
  });
  return { save: (zone: string) => mutation.mutate(zone), saving: mutation.isPending, refusal };
}
