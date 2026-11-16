import { useLocation, useNavigate } from 'react-router-dom';
import { AppRoutes } from '../../AppRoutes';
import { routeHistoryIds } from './routeHistoryIds';

// The app's routes with the router's history in reach (CT only): where the router is, a push to `to` (as a link
// would), and the browser's Back. A spec can then tell a redirect that replaces from one that pushes.
export function RouteHistoryHarness({ to }: { to: string }) {
  const { pathname, search } = useLocation();
  const navigate = useNavigate();
  return (
    <>
      <output data-testid={routeHistoryIds.location}>{pathname + search}</output>
      <button type="button" data-testid={routeHistoryIds.go} onClick={() => void navigate(to)}>
        Go
      </button>
      <button type="button" data-testid={routeHistoryIds.back} onClick={() => void navigate(-1)}>
        Back
      </button>
      <AppRoutes />
    </>
  );
}
