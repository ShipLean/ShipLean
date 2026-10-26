import type { SprintDto } from '../../../api/sprints';
import { boardTestIds as ids } from '../testIds';
import { NONE } from './filterQuery';

// Any sprint, one sprint, or the backlog (no sprint).
export function SprintFilter({
  sprints,
  value,
  onChange,
}: {
  sprints: SprintDto[];
  value: string | undefined;
  onChange: (sprint: string | undefined) => void;
}) {
  return (
    <select
      data-testid={ids.filterSprint}
      className="field"
      aria-label="Sprint"
      value={value ?? ''}
      onChange={(event) => onChange(event.target.value === '' ? undefined : event.target.value)}
    >
      <option value="">Any sprint</option>
      {sprints.map((sprint) => (
        <option key={sprint.id} value={sprint.id}>
          {sprint.name}
        </option>
      ))}
      <option value={NONE}>Backlog (no sprint)</option>
    </select>
  );
}
