import { useNavigate } from 'react-router-dom';
import { routePath, type BacklogMode, type Level } from '../../../routes';
import { backlogTestIds as ids } from '../testIds';

const labels: Record<BacklogMode, string> = { list: 'List', planning: 'Planning', tree: 'Tree' };
const backlogRoute = {
  stories: 'storiesBacklog',
  features: 'featuresBacklog',
  epics: 'epicsBacklog',
} as const satisfies Record<Level, 'storiesBacklog' | 'featuresBacklog' | 'epicsBacklog'>;

// List · Planning · Tree: the mode is the route's ?mode= query, so the switch is a navigation.
export function ModeSwitch({
  projectKey,
  level,
  mode,
  modes,
}: {
  projectKey: string;
  level: Level;
  mode: BacklogMode;
  modes: readonly BacklogMode[];
}) {
  const navigate = useNavigate();
  return (
    <div
      role="group"
      aria-label="Mode"
      data-testid={ids.modeSwitch}
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {modes.map((option) => (
        <button
          key={option}
          type="button"
          data-testid={ids.modeOption}
          data-mode={option}
          aria-pressed={option === mode}
          className={`px-3 text-[length:var(--text-sm)] font-medium ${
            option === mode ? 'bg-signal-soft text-ink' : 'text-ink-muted hover:text-ink'
          }`}
          style={{ height: 'var(--control-height)' }}
          onClick={() =>
            void navigate(routePath(backlogRoute[level], { key: projectKey }, { mode: option }))
          }
        >
          {labels[option]}
        </button>
      ))}
    </div>
  );
}
