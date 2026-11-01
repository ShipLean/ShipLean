import { useQuery } from '@tanstack/react-query';
import { useLocation, useParams } from 'react-router-dom';
import { projectByKeyQuery, sprintsQuery } from '../../../pages/projectQueries';
import { crumbTrail, matchRoute } from '../../../routes';
import { shellTestIds as ids } from '../testIds';

// Where you are: the project key (or ShipLean on install-level pages), then the screen's crumbs. An address that is
// no screen shows just the root.
export function Crumbs() {
  const { pathname, search } = useLocation();
  const key = useParams().key;
  const route = matchRoute(pathname, search);
  // The sprint Retro screen names its sprint ("Sprint 24"), as the design does; the other sprint tabs still say
  // "Sprint". The project and its sprints are the queries the page itself makes, so this reads the cache.
  const sprintId =
    route?.id === 'sprintRetro' ? (route.params as { sprintId: string }).sprintId : undefined;
  const project = useQuery({ ...projectByKeyQuery(key ?? ''), enabled: sprintId !== undefined });
  const sprints = useQuery({
    ...sprintsQuery(project.data?.id ?? ''),
    enabled: sprintId !== undefined && project.data !== undefined,
  });
  const sprintName = sprints.data?.find((s) => s.id === sprintId)?.name;
  const trail = (
    route ? crumbTrail(route.id, route.params as never, route.query) : [key ?? 'ShipLean']
  ).map((crumb, index) => (index === 2 && sprintName !== undefined ? sprintName : crumb));
  return (
    <nav
      data-testid={ids.crumbs}
      aria-label="You are here"
      className="flex min-w-0 items-center gap-1.5 text-[length:var(--text-sm)] text-ink-muted"
    >
      {trail.map((crumb, index) => (
        <span key={`${index}-${crumb}`} className="flex items-center gap-1.5">
          {index > 0 ? <span aria-hidden="true">›</span> : null}
          <span
            data-testid={ids.crumb}
            className={index === trail.length - 1 ? 'font-semibold text-ink' : undefined}
            aria-current={index === trail.length - 1 ? 'page' : undefined}
          >
            {crumb}
          </span>
        </span>
      ))}
    </nav>
  );
}
