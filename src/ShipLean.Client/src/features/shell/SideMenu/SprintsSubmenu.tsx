import { Chip } from '../../../components/Chip';
import { routePath, type MatchedRoute } from '../../../routes';
import { shellTestIds as ids } from '../testIds';
import { useSprints } from '../useShellQueries';
import { NavItem } from './NavItem';

// Under Sprints while you are in Sprints: the current sprint (its backlog) and All sprints.
export function SprintsSubmenu({
  projectId,
  projectKey,
  route,
}: {
  projectId: string;
  projectKey: string;
  route: MatchedRoute | undefined;
}) {
  const current = useSprints(projectId, true).data?.find((sprint) => sprint.status === 'current');
  const inCurrent =
    current !== undefined &&
    route !== undefined &&
    'sprintId' in route.params &&
    route.params.sprintId === current.id;
  return (
    <>
      {current ? (
        <NavItem
          sub
          testId={ids.navCurrentSprint}
          to={routePath('sprintBacklog', { key: projectKey, sprintId: current.id })}
          active={inCurrent}
        >
          {current.name}
          <span className="ml-auto">
            <Chip tone="accent">Current</Chip>
          </span>
        </NavItem>
      ) : null}
      <NavItem
        sub
        testId={ids.navAllSprints}
        to={routePath('sprints', { key: projectKey })}
        active={route?.id === 'sprints'}
      >
        All sprints
      </NavItem>
    </>
  );
}
