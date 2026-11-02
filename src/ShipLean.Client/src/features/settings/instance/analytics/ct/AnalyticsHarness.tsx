import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { InstanceAnalytics } from '../InstanceAnalytics';

// Mounts the tab for component tests; call stubAnalyticsApi(page) from ./analyticsStub first so the C4 GET is answered.
export function AnalyticsHarness() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <InstanceAnalytics />
    </QueryClientProvider>
  );
}
