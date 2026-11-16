import { useRef, useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { CloseSprintSlot } from '../../../slots/CloseSprintSlot';
import { EditSprintRailSlot } from '../../../slots/EditSprintRailSlot';
import { useSprintFigures, useSprintsRealtime, useVelocity } from '../sprintsApi';
import { useTouchShell } from '../../../touch/useTouchShell';
import { sprintsTestIds as ids } from '../testIds';
import { orderSprints } from './orderSprints';
import { SprintRow } from './SprintRow';
import { SprintsEmpty } from './SprintsEmpty';
import { VelocityCard } from './VelocityCard';

const HEADINGS = ['Sprint', 'Dates', 'Status', 'Goal'] as const;
const NUMBER_HEADINGS = ['Team capacity', 'Committed', 'Completed'] as const;
// At 900px and below the sprints sit in sections in this order, under these headings (SHIP-1807 P2).
const SECTIONS = [
  { status: 'current', heading: 'Current' },
  { status: 'future', heading: 'Future' },
  { status: 'past', heading: 'Past' },
] as const;

/** The All sprints body: the velocity chart, then every sprint (current, future, past) with its figures. */
export function SprintsList({ project, sprints }: { project: ProjectDto; sprints: SprintDto[] }) {
  const { touch } = useTouchShell();
  useSprintsRealtime(project.id);
  const figures = useSprintFigures(project.id, sprints);
  const velocity = useVelocity(project.id).data;
  // The sprint whose Edit sprint rail is open above the table.
  const [editingId, setEditingId] = useState<string | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);
  const editing = sprints.find((sprint) => sprint.id === editingId);
  const planningVelocity =
    velocity === undefined ? undefined : (velocity.override ?? velocity.average);
  // The sprint whose Close sprint dialog is open (the touch row's ⋯ sheet).
  const [closingId, setClosingId] = useState<string | null>(null);
  const closing = sprints.find((sprint) => sprint.id === closingId);
  if (touch && sprints.length === 0) return <SprintsEmpty projectId={project.id} />;
  const ordered = orderSprints(sprints);
  const row = (sprint: SprintDto) => (
    <SprintRow
      key={sprint.id}
      projectKey={project.key}
      sprint={sprint}
      figures={figures.get(sprint.id)}
      velocity={planningVelocity}
      onEdit={(button) => {
        trigger.current = button;
        setEditingId(sprint.id);
      }}
      onCloseSprint={() => setClosingId(sprint.id)}
    />
  );
  return (
    <div className="grid content-start gap-4">
      <VelocityCard projectId={project.id} />
      {closing === undefined ? null : (
        <CloseSprintSlot
          project={project}
          sprint={closing}
          open
          onClose={() => setClosingId(null)}
        />
      )}
      {editing === undefined ? null : (
        <EditSprintRailSlot
          project={project}
          sprint={editing}
          onDone={() => {
            setEditingId(null);
            trigger.current?.focus();
          }}
          onDeleted={() => setEditingId(null)}
        />
      )}
      {touch ? (
        <div data-testid={ids.listTouch} className="grid gap-3">
          {SECTIONS.map(({ status, heading }) => {
            const inSection = ordered.filter((sprint) => sprint.status === status);
            if (inSection.length === 0) return null;
            return (
              <section key={status} data-testid={ids.listSection} data-section={status}>
                <h2 className="m-0 mb-1.5 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
                  {heading}
                  {status === 'current' ? null : (
                    <span className="font-normal tracking-normal"> · {inSection.length}</span>
                  )}
                </h2>
                <ul className="m-0 list-none overflow-hidden rounded-md border border-edge bg-panel p-0">
                  {inSection.map((sprint) => row(sprint))}
                </ul>
              </section>
            );
          })}
        </div>
      ) : (
        <section className="overflow-x-auto rounded-md border border-edge bg-panel">
          <table data-testid={ids.list} className="w-full border-collapse text-left">
            <thead>
              <tr className="bg-sunken text-[length:var(--text-xs)] text-ink-muted">
                {HEADINGS.map((heading) => (
                  <th key={heading} className="px-3 py-2 font-semibold">
                    {heading}
                  </th>
                ))}
                {NUMBER_HEADINGS.map((heading) => (
                  <th key={heading} className="px-3 py-2 text-right font-semibold">
                    {heading}
                  </th>
                ))}
                <th className="px-3 py-2">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {ordered.map((sprint) => row(sprint))}
            </tbody>
          </table>
        </section>
      )}
    </div>
  );
}
