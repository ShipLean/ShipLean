import { useRef, useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { EditSprintRailSlot } from '../../../slots/EditSprintRailSlot';
import { useSprintFigures, useSprintsRealtime, useVelocity } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';
import { orderSprints } from './orderSprints';
import { SprintRow } from './SprintRow';
import { VelocityCard } from './VelocityCard';

const HEADINGS = ['Sprint', 'Dates', 'Status', 'Goal'] as const;
const NUMBER_HEADINGS = ['Team capacity', 'Committed', 'Completed'] as const;

/** The All sprints body: the velocity chart, then every sprint (current, future, past) with its figures. */
export function SprintsList({ project, sprints }: { project: ProjectDto; sprints: SprintDto[] }) {
  useSprintsRealtime(project.id);
  const figures = useSprintFigures(project.id, sprints);
  const velocity = useVelocity(project.id).data;
  // The sprint whose Edit sprint rail is open above the table.
  const [editingId, setEditingId] = useState<string | null>(null);
  const trigger = useRef<HTMLButtonElement | null>(null);
  const editing = sprints.find((sprint) => sprint.id === editingId);
  const planningVelocity =
    velocity === undefined ? undefined : (velocity.override ?? velocity.average);
  return (
    <div className="grid content-start gap-4">
      <VelocityCard projectId={project.id} />
      {editing === undefined ? null : (
        <EditSprintRailSlot
          project={project}
          sprint={editing}
          onDone={() => {
            setEditingId(null);
            trigger.current?.focus();
          }}
        />
      )}
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
            {orderSprints(sprints).map((sprint) => (
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
              />
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
