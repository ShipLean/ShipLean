import { useMe } from '../../shell/useShellQueries';
import type { QuickSearchResults } from '../types';
import { readRecent } from './recent/recentItems';

const SHOWN = 5;

// The items opened in this browser. Storage is read while rendering, with no state: the host remounts the sources on
// every open, so an item opened a moment ago shows.
export function useRecentSource(): QuickSearchResults {
  const userId = useMe().data?.id;
  const items = userId === undefined ? [] : readRecent(userId).slice(0, SHOWN);
  return {
    groups:
      items.length === 0
        ? []
        : [
            {
              id: 'recent',
              title: 'Recent',
              rows: items.map((item) => ({ kind: 'item', id: item.key, item, match: null })),
            },
          ],
    status: 'ready',
    note: 'Recent items are kept in this browser',
    retry: null,
  };
}
