import { routePath, type MatchedRoute, type RouteId } from '../../../routes';
import { shellTestIds as ids } from '../testIds';
import { NavItem } from './NavItem';

const LEVELS = {
  backlogs: [
    { label: 'Epics', route: 'epicsBacklog' },
    { label: 'Features', route: 'featuresBacklog' },
    { label: 'Stories', route: 'storiesBacklog' },
  ],
  boards: [
    { label: 'Epics', route: 'epicsBoard' },
    { label: 'Features', route: 'featuresBoard' },
    { label: 'Stories', route: 'storiesBoard' },
  ],
} as const satisfies Record<string, readonly { label: string; route: RouteId }[]>;

// Under Backlogs or Boards while you are in that section: Epics, Features and Stories.
export function LevelsSubmenu({
  section,
  projectKey,
  route,
}: {
  section: keyof typeof LEVELS;
  projectKey: string;
  route: MatchedRoute | undefined;
}) {
  return (
    <>
      {LEVELS[section].map((level) => (
        <NavItem
          key={level.route}
          sub
          testId={ids.navLevel}
          to={routePath(level.route, { key: projectKey })}
          active={route?.id === level.route}
        >
          {level.label}
        </NavItem>
      ))}
    </>
  );
}
