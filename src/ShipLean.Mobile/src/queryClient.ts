import { QueryClient } from '@tanstack/react-query';

// The app's one query cache. Focus refetching stays off: a phone comes back to the foreground far more often than a
// web tab regains focus, and pull-to-refresh is the way to ask for fresh data.
export const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
});
