import { useLocation } from 'react-router-dom';
import { ProjectRealtimeStatus } from '../../realtime/ProjectRealtimeStatus';
import { isInstanceLevel, matchRoute } from '../../routes';

// The Live dot and who else is on this screen, on project routes; nothing on install-level ones.
export function TopBarRealtime() {
  const { pathname, search } = useLocation();
  const route = matchRoute(pathname, search);
  if (route === undefined || isInstanceLevel(route.id) || !('key' in route.params)) return null;
  return <ProjectRealtimeStatus routeId={route.id} projectKey={route.params.key} />;
}
