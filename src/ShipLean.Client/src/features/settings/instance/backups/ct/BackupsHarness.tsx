import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { InstanceBackups } from '../InstanceBackups';

// Mounts the tab for component tests; call stubBackupsApi(page) from ./backupsStub first so the GET is answered.
export function BackupsHarness() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <InstanceBackups />
    </QueryClientProvider>
  );
}
