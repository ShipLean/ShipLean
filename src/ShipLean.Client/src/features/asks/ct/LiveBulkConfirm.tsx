import { useQuery } from '@tanstack/react-query';
import { asksKey, fetchAsks } from '../asksApi';
import { workItemCommentsKey } from '../../workItem/workItemApi';
import { BulkConfirm } from '../BulkConfirm';
import { asksState, type AsksItem } from '../regions';
import type { ItemAsksDto } from '@shiplean/core/api/asks';

// BulkConfirm fed the way the section feeds it: from the asks query, so the refetch after a write changes its props
// and, once the section folds, unmounts it. Playwright CT needs this as its own file because the spec can only mount imported components.
export function LiveBulkConfirm({ item, initial }: { item: AsksItem; initial: ItemAsksDto }) {
  const { data } = useQuery({
    queryKey: asksKey(item.projectId, item.key),
    queryFn: () => fetchAsks(item.id),
    initialData: initial,
    staleTime: Infinity,
  });
  // The open Discussion on the page: its refetch is slower than the asks one, so the asks land (and unmount the button)
  // while the write's refetch is still pending.
  useQuery({
    queryKey: workItemCommentsKey(item.projectId, item.key),
    queryFn: () => new Promise<string>((resolve) => setTimeout(() => resolve('comments'), 400)),
    staleTime: Infinity,
  });
  // As the section folds once nothing is waiting, AsksHeader stops rendering BulkConfirm: it unmounts.
  return asksState(data) === 'settled' ? null : <BulkConfirm item={item} asks={data} />;
}
