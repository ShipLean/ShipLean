import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { MeDto } from '../../../../api/me';
import { meKey } from '../../../../queries/shared';
import { applyAppearance } from '../../../../theme';
import { savePreference } from '../../settingsApi';
import { errorText } from './errorText';

const APPEARANCE_KEY = ['appearance'] as const;

export interface AppearanceChoice {
  key: 'theme' | 'density';
  value: string;
}

/**
 * Saves a theme or density pick: shown at once, stored on the account, and put back if the server refuses.
 * Saves run one at a time, so the server ends on the last pick. A refusal puts back only its own setting, and only
 * while the cache still shows what that pick set; the settled save then reads the server's truth.
 * `onRefused` runs from the mutation's own callback, so it still runs when the caller unmounted after the pick.
 */
export function useAppearanceSave({ onRefused }: { onRefused?: () => void } = {}) {
  const queryClient = useQueryClient();
  const [refusal, setRefusal] = useState<string | null>(null);
  const mutation = useMutation({
    mutationKey: APPEARANCE_KEY,
    scope: { id: 'appearance' },
    mutationFn: ({ key, value }: AppearanceChoice) => savePreference(key, value),
    onMutate: async ({ key, value }) => {
      setRefusal(null);
      await queryClient.cancelQueries({ queryKey: meKey });
      const previous = queryClient.getQueryData<MeDto>(meKey);
      if (previous !== undefined) {
        const next = { ...previous, [key]: value } as MeDto;
        queryClient.setQueryData(meKey, next);
        applyAppearance(next.theme, next.density);
      }
      return { before: previous?.[key] };
    },
    onError: (error, { key, value }, context) => {
      setRefusal(errorText(error, 'That choice could not be saved.'));
      const current = queryClient.getQueryData<MeDto>(meKey);
      try {
        if (current !== undefined && context?.before !== undefined && current[key] === value) {
          const restored = { ...current, [key]: context.before } as MeDto;
          queryClient.setQueryData(meKey, restored);
          applyAppearance(restored.theme, restored.density);
        }
      } finally {
        onRefused?.();
      }
    },
    onSettled: () => {
      if (queryClient.isMutating({ mutationKey: APPEARANCE_KEY }) <= 1) {
        void queryClient.invalidateQueries({ queryKey: meKey });
      }
    },
  });
  return {
    save: (key: AppearanceChoice['key'], value: string) => mutation.mutate({ key, value }),
    refusal,
  };
}
