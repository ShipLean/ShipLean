import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';
import { CronField } from '../CronField';

// Mounts the cron field alone with a fixed server error, and shows the validity it reports.
export function CronFieldHarness({ initial = '', error }: { initial?: string; error?: string }) {
  const [client] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: false } } }),
  );
  const [cron, setCron] = useState(initial);
  const [valid, setValid] = useState<boolean | null>(null);
  return (
    <QueryClientProvider client={client}>
      <CronField value={cron} onChange={setCron} error={error} onValidityChange={setValid} />
      <output data-testid="cron-harness-valid">{String(valid)}</output>
    </QueryClientProvider>
  );
}
