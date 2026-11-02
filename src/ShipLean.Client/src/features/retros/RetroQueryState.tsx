import type { UseQueryResult } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// A retrospective panel's read: nothing while it loads, an alert when it never loaded, else the body. A failed refetch
// keeps the data on screen.
export function RetroQueryState<T>({
  query,
  children,
}: {
  query: UseQueryResult<T>;
  children: (data: T) => ReactNode;
}) {
  if (query.data === undefined) {
    return query.isError ? (
      <p role="alert" className="p-4 text-alert">
        {query.error.message}
      </p>
    ) : null;
  }
  return <>{children(query.data)}</>;
}
