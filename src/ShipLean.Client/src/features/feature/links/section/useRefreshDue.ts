import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { LinkCardsDto } from '../../../../api/linkCards';
import { linkCardsKey, refreshLinkCards } from '../../featureApi';

/** Once per mount, when any loaded card is due, asks the server to read those again and takes its answer as the list. */
export function useRefreshDue(
  projectId: string,
  itemKey: string,
  workItemId: string,
  cards: LinkCardsDto | undefined,
) {
  const queryClient = useQueryClient();
  const asked = useRef(false);
  const refresh = useMutation({
    mutationFn: () => refreshLinkCards(workItemId),
    onSuccess: (fresh) => queryClient.setQueryData(linkCardsKey(projectId, itemKey), fresh),
  });
  const due = cards ? [...cards.code, ...cards.pages].some((card) => card.refreshDue) : false;
  const { mutate } = refresh;
  useEffect(() => {
    if (!due || asked.current) return;
    asked.current = true;
    mutate();
  }, [due, mutate]);
}
