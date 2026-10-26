import { useQuery } from '@tanstack/react-query';
import { Navigate } from 'react-router-dom';
import { routePath } from '../../routes';
import { fetchMe, fetchProjects, meKey, projectsKey } from '../../queries/shared';

// What "/" opens: the most recent project's Stories backlog, or the no-projects state when there is none. Someone
// who is not signed in is sent to /login by the API client.
export function RootRedirect() {
  const me = useQuery({ queryKey: meKey, queryFn: fetchMe, retry: false });
  const hasProjects = me.data?.hasProjects === true;
  const projects = useQuery({
    queryKey: projectsKey,
    queryFn: fetchProjects,
    retry: false,
    enabled: hasProjects,
  });

  if (me.data && !hasProjects) return <Navigate to="/login?state=no-projects" replace />;
  if (projects.data) {
    const first = projects.data[0];
    return first ? (
      <Navigate to={routePath('storiesBacklog', { key: first.key })} replace />
    ) : (
      <Navigate to="/login?state=no-projects" replace />
    );
  }
  return null;
}
