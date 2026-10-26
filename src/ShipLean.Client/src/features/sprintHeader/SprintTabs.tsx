import { Link } from 'react-router-dom';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { routePath } from '../../routes';
import type { SprintTab } from './SprintHeader';
import { sprintHeaderTestIds as ids } from './testIds';

export function SprintTabs({
  project,
  sprint,
  tab,
}: {
  project: ProjectDto;
  sprint: SprintDto;
  tab: SprintTab;
}) {
  const params = { key: project.key, sprintId: sprint.id };
  const tabs = [
    {
      id: 'backlog',
      label: 'Backlog',
      testId: ids.tabBacklog,
      to: routePath('sprintBacklog', params),
    },
    {
      id: 'taskboard',
      label: 'Taskboard',
      testId: ids.tabTaskboard,
      to: routePath('taskboard', params),
    },
    {
      id: 'capacity',
      label: 'Capacity',
      testId: ids.tabCapacity,
      to: routePath('capacity', params),
    },
  ] as const;
  return (
    <nav
      data-testid={ids.tabs}
      aria-label="Sprint"
      className="flex gap-[var(--space-4)] border-b border-edge"
    >
      {tabs.map((t) => (
        <Link
          key={t.id}
          data-testid={t.testId}
          to={t.to}
          aria-current={t.id === tab ? 'page' : undefined}
          className={`-mb-px border-b-2 px-1 py-1.5 text-[length:var(--text-sm)] font-medium ${
            t.id === tab ? 'border-signal text-ink' : 'border-transparent text-ink-muted'
          }`}
        >
          {t.label}
        </Link>
      ))}
    </nav>
  );
}
