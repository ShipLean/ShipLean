import { configurePlatform } from '@shiplean/core/platform';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { mobilePlatform } from '../mobilePlatform';
import type { Scheme } from '../theme/themes';
import { setScheme } from './colorScheme';
import { phoneMetrics } from './phone';

export interface RenderScreenOptions {
  scheme?: Scheme;
}

// One screen or primitive on a 390×844 notched phone, with its own query cache (no retries, nothing collected) and
// the app's platform, so requests go through the real core client to the fakeApi installed at fetch.
export async function renderScreen(
  ui: ReactElement,
  { scheme = 'light' }: RenderScreenOptions = {},
) {
  setScheme(scheme);
  configurePlatform(mobilePlatform);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  });
  const result = await render(
    <SafeAreaProvider initialMetrics={phoneMetrics}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </SafeAreaProvider>,
  );
  return Object.assign(result, { queryClient });
}
