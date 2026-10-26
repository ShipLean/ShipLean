import { useId } from 'react';
import type { SprintRef } from '../../../api/refs';
import { workItemTestIds as ids } from '../testIds';
import { useProjectSprints } from '../workItemApi';
import { FieldCell } from './FieldCell';
import { selectClass } from './inputClasses';

const BACKLOG = '';

export function SprintField({
  projectId,
  value,
  changed,
  onChange,
}: {
  projectId: string;
  value: SprintRef | null;
  changed: boolean;
  onChange: (sprint: SprintRef | null) => void;
}) {
  const selectId = useId();
  const sprints = useProjectSprints(projectId);
  const open = (sprints.data ?? []).filter((sprint) => sprint.status !== 'past');
  const options: SprintRef[] = [...open];
  if (value && !options.some((sprint) => sprint.id === value.id)) options.unshift(value);
  return (
    <FieldCell label="Sprint" field="sprint" changed={changed} htmlFor={selectId}>
      <select
        id={selectId}
        data-testid={ids.sprint}
        className={selectClass}
        value={value?.id ?? BACKLOG}
        onChange={(event) =>
          onChange(options.find((sprint) => sprint.id === event.target.value) ?? null)
        }
      >
        <option value={BACKLOG}>Backlog</option>
        {options.map((sprint) => (
          <option key={sprint.id} value={sprint.id}>
            {sprint.name}
            {sprint.status === 'current' ? ' (current)' : ''}
          </option>
        ))}
      </select>
    </FieldCell>
  );
}
