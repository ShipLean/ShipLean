import { Link } from 'react-router-dom';
import { routePath, type Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';

const OPTIONS = [
  { level: 'epics', label: 'Epics', route: 'epicsBoard' },
  { level: 'features', label: 'Features', route: 'featuresBoard' },
  { level: 'stories', label: 'Stories', route: 'storiesBoard' },
] as const satisfies readonly {
  level: Level;
  label: string;
  route: 'epicsBoard' | 'featuresBoard' | 'storiesBoard';
}[];

// The level switch of the touch toolbar: Epics · Features · Stories, each a link to that level's board route.
export function LevelSwitch({ level, projectKey }: { level: Level; projectKey: string }) {
  return (
    <nav
      aria-label="Board level"
      data-testid={ids.levelSwitch}
      className="inline-flex flex-none overflow-hidden rounded-control border border-edge"
    >
      {OPTIONS.map((option) => (
        <Link
          key={option.level}
          to={routePath(option.route, { key: projectKey })}
          data-testid={ids.levelOption}
          data-level={option.level}
          aria-current={option.level === level ? 'page' : undefined}
          className={`inline-flex min-h-[var(--touch-target)] items-center px-2.5 text-[length:var(--text-sm)] font-medium no-underline ${
            option.level === level ? 'bg-signal-soft text-ink' : 'text-ink-muted'
          }`}
        >
          {option.label}
        </Link>
      ))}
    </nav>
  );
}
