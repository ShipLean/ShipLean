import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { InstanceAnalytics } from '../InstanceAnalytics';

// Mounts the tab for component tests; the spec routes /api/instance/analytics in the C4 shape.
export function AnalyticsHarness() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <InstanceAnalytics />
    </QueryClientProvider>
  );
}
