import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { AppRoutes } from './AppRoutes';
import { retryUnlessClientError } from './queries/retry';
import { initAppearance } from './theme';
import '@fontsource-variable/inter';
import './index.css';

initAppearance();
const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: retryUnlessClientError } },
});
// A data router (not BrowserRouter) so a screen can block navigation while it holds unsaved changes (useBlocker);
// AppRoutes is the whole route table beneath it.
const router = createBrowserRouter([{ path: '*', element: <AppRoutes /> }]);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
