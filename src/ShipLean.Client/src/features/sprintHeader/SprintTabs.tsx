import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
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
  const nav = useRef<HTMLElement>(null);
  // At 900px and below the tabs scroll sideways in their own box: bring the current one into view, without scrolling
  // the page. Above 900px every tab fits, so this changes nothing.
  useEffect(() => {
    const box = nav.current;
    const current = box?.querySelector<HTMLElement>('[aria-current="page"]');
    if (!box || !current) return;
    const right = current.offsetLeft + current.offsetWidth;
    if (right > box.scrollLeft + box.clientWidth) box.scrollLeft = right - box.clientWidth;
    else if (current.offsetLeft < box.scrollLeft) box.scrollLeft = current.offsetLeft;
  }, [tab]);
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
    {
      id: 'retro',
      label: 'Retro',
      testId: ids.tabRetro,
      to: routePath('sprintRetro', params),
    },
    {
      id: 'demo',
      label: 'Demo',
      testId: ids.tabDemo,
      to: routePath('sprintDemo', params),
    },
  ] as const;
  return (
    <nav
      ref={nav}
      data-testid={ids.tabs}
      aria-label="Sprint"
      className="relative flex gap-[var(--space-4)] border-b border-edge touch:gap-0.5 touch:overflow-x-auto touch:border-b-0 touch:shadow-[inset_0_-1px_0_var(--color-border)] touch:[scrollbar-width:none]"
    >
      {tabs.map((t) => (
        <Link
          key={t.id}
          data-testid={t.testId}
          to={t.to}
          aria-current={t.id === tab ? 'page' : undefined}
          className={`-mb-px border-b-2 px-1 py-1.5 text-[length:var(--text-sm)] font-medium touch:mb-0 touch:inline-flex touch:h-[38px] touch:flex-none touch:items-center touch:px-2.5 touch:py-0 ${
            t.id === tab ? 'border-signal text-ink' : 'border-transparent text-ink-muted'
          }`}
        >
          {t.label}
        </Link>
      ))}
    </nav>
  );
}
