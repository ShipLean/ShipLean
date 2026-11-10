import {
  featuresKeys,
  getFeatureDigest,
  type FeatureDigestDto,
} from '@shiplean/core/api/featuresOverview';
import { useQueries } from '@tanstack/react-query';
import { useCallback, useState } from 'react';

export type DigestTail = 'end' | 'more' | 'loading' | 'error';

export interface DigestPages {
  // The first page's read, for the screen's own loading and error states.
  first: { isError: boolean; isFetching: boolean; refetch: () => void };
  // Every page that has loaded, in order.
  pages: FeatureDigestDto[];
  // What sits below the last loaded page.
  tail: DigestTail;
  loadMore: () => void;
  retryTail: () => void;
}

// The digest read a page at a time: the first page, then one more for each cursor asked for. Each page keeps its own
// query under the overview's root, so a seen invalidates them all.
export function useDigestPages(projectId: string): DigestPages {
  const [cursors, setCursors] = useState<string[]>([]);
  const results = useQueries({
    queries: [null, ...cursors].map((cursor) => ({
      queryKey: featuresKeys.digest(projectId, cursor),
      queryFn: () => getFeatureDigest(projectId, cursor),
    })),
  });
  const pages: FeatureDigestDto[] = [];
  for (const r of results) {
    if (r.data === undefined) break;
    pages.push(r.data);
  }
  const last = results[results.length - 1];
  const lastPage = pages[pages.length - 1];
  const next = lastPage?.more === true ? lastPage.cursor : null;

  let tail: DigestTail = 'end';
  if (pages.length < results.length) tail = last.isError ? 'error' : 'loading';
  else if (next !== null) tail = 'more';

  const loadMore = useCallback(() => {
    if (next === null) return;
    setCursors((all) => (all.includes(next) ? all : [...all, next]));
  }, [next]);

  const first = results[0];
  return {
    first: {
      isError: first.isError,
      isFetching: first.isFetching,
      refetch: () => void first.refetch(),
    },
    pages,
    tail,
    loadMore,
    retryTail: () => void last.refetch(),
  };
}
