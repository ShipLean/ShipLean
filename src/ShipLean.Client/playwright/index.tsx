import { beforeMount } from '@playwright/experimental-ct-react/hooks';
import { configurePlatform } from '@shiplean/core/platform';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import '@fontsource-variable/inter';
import '../src/index.css';
import { webPlatform } from '../src/webPlatform';

// The web platform, as main.tsx configures it.
configurePlatform(webPlatform);

type HooksConfig = {
  route?: string;
};

beforeMount<HooksConfig>(async ({ App, hooksConfig }) => {
  // A fresh query cache per mount, no retries: a test stubs the API with page.route and sees its answer at once.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // The same shape as main.tsx: a data router with one splat route holding the mounted component.
  const router = createMemoryRouter([{ path: '*', element: <App /> }], {
    initialEntries: [hooksConfig?.route ?? '/'],
  });
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
});
