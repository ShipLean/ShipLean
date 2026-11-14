import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '@shiplean/core/api/client';
import { renameRetroTheme } from '@shiplean/core/api/retrosApi';
import { retrosRootKey } from '../../useRetroQueries';

const TAKEN = 'That name is taken';

export interface RenameTheme {
  /** PUT api/retros/themes/{id}. A blank or unchanged name is the caller's to refuse. */
  rename: (name: string) => void;
  pending: boolean;
  /** "That name is taken" for a 409, otherwise the failure's message; null while there is none. */
  error: string | null;
  clearError: () => void;
}

/**
 * Renames a theme everywhere (`PUT api/retros/themes/{id}`) and refreshes every retro view. The desktop ✎ and the
 * phone's theme sheet share it; `onRenamed` runs once the server has taken the new name.
 */
export function useRenameTheme(
  projectId: string,
  themeId: string,
  onRenamed?: () => void,
): RenameTheme {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const mutation = useMutation({
    mutationFn: (name: string) => renameRetroTheme(themeId, { name }),
    onSuccess: async () => {
      setError(null);
      onRenamed?.();
      await queryClient.invalidateQueries({ queryKey: retrosRootKey(projectId) });
    },
    onError: (failure) => {
      setError(
        failure instanceof ApiRequestError && failure.status === 409 ? TAKEN : failure.message,
      );
    },
  });
  return {
    rename: (name) => mutation.mutate(name),
    pending: mutation.isPending,
    error,
    clearError: () => setError(null),
  };
}
