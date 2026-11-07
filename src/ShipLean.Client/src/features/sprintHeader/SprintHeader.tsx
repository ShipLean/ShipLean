import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { routePath } from '../../routes';
import { CloseSprintSlot } from '../../slots/CloseSprintSlot';
import { EditSprintRailSlot } from '../../slots/EditSprintRailSlot';
import { SprintGoalEditSlot } from '../../slots/SprintGoalEditSlot';
import { SprintGoal } from './SprintGoal';
import { SprintPicker } from './SprintPicker';
import { SprintStatusChip } from './SprintStatusChip';
import { SprintTabs } from './SprintTabs';
import { sprintHeaderTestIds as ids } from './testIds';

export type SprintTab = 'backlog' | 'taskboard' | 'capacity' | 'retro' | 'demo';

export interface SprintHeaderProps {
  // full: Sprint backlog and Capacity (with the Backlog · Taskboard · Capacity · Retro tabs); slim: the Taskboard.
  variant: 'full' | 'slim';
  project: ProjectDto;
  sprint: SprintDto;
  // Every sprint of the project, for the sprint picker.
  sprints: SprintDto[];
  tab: SprintTab;
}

export function SprintHeader({ variant, project, sprint, sprints, tab }: SprintHeaderProps) {
  const slim = variant === 'slim';
  const navigate = useNavigate();
  // The sprint whose Edit sprint rail is open; moving to another sprint closes it.
  const [editingId, setEditingId] = useState<string | null>(null);
  const editSprintRef = useRef<HTMLButtonElement>(null);
  const picker = <SprintPicker project={project} sprint={sprint} sprints={sprints} tab={tab} />;
  const chip = <SprintStatusChip status={sprint.status} />;
  const closeSprint = <CloseSprintSlot project={project} sprint={sprint} />;
  const allSprints = (
    <Link
      data-testid={ids.allSprints}
      className="text-[length:var(--text-sm)]"
      to={routePath('sprints', { key: project.key })}
    >
      All sprints
    </Link>
  );
  const when = (
    <div className="flex flex-wrap items-center gap-[var(--space-2)] text-[length:var(--text-sm)] text-ink-muted">
      <span data-testid={ids.dates}>{sprint.datesLabel}</span>
      <span aria-hidden="true">·</span>
      <b data-testid={ids.timeLeft} className="font-semibold text-ink">
        {sprint.timeLabel}
      </b>
    </div>
  );
  return (
    <section
      data-testid={ids.header}
      aria-label={sprint.name}
      className="flex flex-col gap-[var(--space-2)]"
    >
      {slim ? (
        <div className="flex flex-wrap items-center gap-[var(--space-3)]">
          {picker}
          {chip}
          {when}
          <span className="ml-auto">{allSprints}</span>
          {closeSprint}
        </div>
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-[var(--space-3)]">
            {picker}
            {chip}
            {allSprints}
            <span className="ml-auto">{closeSprint}</span>
          </div>
          {when}
          <SprintGoal
            key={sprint.id}
            project={project}
            sprint={sprint}
            sprints={sprints}
            Editor={SprintGoalEditSlot}
            onEditSprint={() => setEditingId(sprint.id)}
            onEditGoal={() => setEditingId(null)}
            editSprintRef={editSprintRef}
          />
          {editingId === sprint.id ? (
            <EditSprintRailSlot
              project={project}
              sprint={sprint}
              onDone={() => {
                setEditingId(null);
                editSprintRef.current?.focus();
              }}
              // This page's sprint is gone, so the person lands on the list of the ones left.
              onDeleted={() => void navigate(routePath('sprints', { key: project.key }))}
            />
          ) : null}
          <SprintTabs project={project} sprint={sprint} tab={tab} />
        </>
      )}
    </section>
  );
}
