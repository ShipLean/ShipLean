import { boardTestIds as ids } from '../testIds';
import { NONE } from './filterQuery';

// Any Epic, one Epic (a Feature matches by its nearest Epic above it), or no Epic.
export function EpicFilter({
  epics,
  value,
  onChange,
}: {
  epics: { id: string; title: string }[];
  value: string | undefined;
  onChange: (epic: string | undefined) => void;
}) {
  return (
    <select
      data-testid={ids.filterEpic}
      className="field"
      aria-label="Epic"
      value={value ?? ''}
      onChange={(event) => onChange(event.target.value === '' ? undefined : event.target.value)}
    >
      <option value="">Any Epic</option>
      {epics.map((epic) => (
        <option key={epic.id} value={epic.id}>
          {epic.title}
        </option>
      ))}
      <option value={NONE}>No Epic</option>
    </select>
  );
}
