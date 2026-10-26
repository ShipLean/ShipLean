import { Link } from 'react-router-dom';
import { routePath, type Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';

const LEVELS = [
  { level: 'epics', label: 'Epics', route: 'epicsBoard' },
  { level: 'features', label: 'Features', route: 'featuresBoard' },
  { level: 'stories', label: 'Stories', route: 'storiesBoard' },
] as const;

// Epics · Features · Stories: the three boards of the project.
export function LevelSwitch({ projectKey, level }: { projectKey: string; level: Level }) {
  return (
    <nav
      data-testid={ids.levelSwitch}
      aria-label="Board level"
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {LEVELS.map((option) => (
        <Link
          key={option.level}
          to={routePath(option.route, { key: projectKey })}
          data-testid={ids.levelOption}
          data-level={option.level}
          aria-current={option.level === level ? 'page' : undefined}
          className={`inline-flex h-7 items-center px-3 font-medium ${
            option.level === level ? 'bg-signal-soft text-ink' : 'text-ink-muted hover:text-ink'
          }`}
        >
          {option.label}
        </Link>
      ))}
    </nav>
  );
}
