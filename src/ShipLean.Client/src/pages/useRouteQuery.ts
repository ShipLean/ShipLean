import { useLocation } from 'react-router-dom';
import { matchRoute, type RouteQuery } from '../routes';

// The query string of the open address as routes.ts understands it (unknown parts dropped, a bad mode ignored).
export function useRouteQuery(): RouteQuery {
  const { pathname, search } = useLocation();
  return matchRoute(pathname, search)?.query ?? {};
}
