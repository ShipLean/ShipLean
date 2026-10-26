import { useNavigate } from 'react-router-dom';
import { routePath, type Level } from '../../../routes';
import { backlogTestIds as ids } from '../testIds';

const options = [
  { level: 'epics', label: 'Epics', route: 'epicsBacklog' },
  { level: 'features', label: 'Features', route: 'featuresBacklog' },
  { level: 'stories', label: 'Stories', route: 'storiesBacklog' },
] as const satisfies readonly { level: Level; label: string; route: string }[];

// Epics · Features · Stories: the level is part of the address, so choosing one is a navigation.
export function LevelPicker({ projectKey, level }: { projectKey: string; level: Level }) {
  const navigate = useNavigate();
  return (
    <div
      role="group"
      aria-label="Backlog level"
      data-testid={ids.levelPicker}
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {options.map((option) => (
        <button
          key={option.level}
          type="button"
          data-testid={ids.levelOption}
          data-level={option.level}
          aria-pressed={option.level === level}
          className={`px-3 text-[length:var(--text-sm)] font-medium ${
            option.level === level ? 'bg-signal-soft text-ink' : 'text-ink-muted hover:text-ink'
          }`}
          style={{ height: 'var(--control-height)' }}
          onClick={() => void navigate(routePath(option.route, { key: projectKey }))}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
