import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '../../../../api/client';
import type { MeDto } from '../../../../api/me';
import { showToast } from '../../../../components/toastStore';
import { meKey } from '../../../../queries/shared';
import { removePhoto, saveProfile, uploadPhoto } from '../../settingsApi';
import type { IdentityDraft } from './model';

/**
 * Applies the draft: the name and colour in one request when either changed, then the photo (upload or remove). Each
 * step's answer is the caller's MeDto, so the cache gets the last one at once; ['me'] is invalidated whatever happens
 * (a step may have landed before a later one failed), and every other query that shows this person is marked stale.
 */
export function useSaveIdentity(me: MeDto, onSaved: () => void) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async (draft: IdentityDraft) => {
      let latest: MeDto | undefined;
      try {
        if (draft.name.trim() !== me.name || draft.avatarColour !== me.avatarColour) {
          latest = await saveProfile({ name: draft.name, avatarColour: draft.avatarColour });
          queryClient.setQueryData(meKey, latest);
        }
        if (draft.pickedFile !== null) {
          latest = await uploadPhoto(draft.pickedFile.file);
        } else if (draft.removePhoto) {
          latest = await removePhoto();
        }
      } finally {
        if (latest !== undefined) queryClient.setQueryData(meKey, latest);
        await queryClient.invalidateQueries({ queryKey: meKey });
        await queryClient.invalidateQueries({
          predicate: (query) => query.queryKey[0] !== meKey[0],
          refetchType: 'none',
        });
      }
    },
    onSuccess: () => {
      setError(null);
      showToast('Profile saved. Everyone sees the change straight away', { tone: 'ok' });
      onSaved();
    },
    onError: (e) =>
      setError(e instanceof ApiRequestError ? e.message : 'Could not save your profile.'),
  });

  return {
    save: mutation.mutate,
    saving: mutation.isPending,
    error,
    clearError: () => setError(null),
  };
}
