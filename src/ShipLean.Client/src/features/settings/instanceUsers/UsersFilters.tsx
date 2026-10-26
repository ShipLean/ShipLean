import { settingsTestIds } from '../testIds';
import type { UsersFilter } from './model';

const filterIds: Record<UsersFilter, string> = {
  all: settingsTestIds.usersFilterAll,
  active: settingsTestIds.usersFilterActive,
  attention: settingsTestIds.usersFilterAttention,
  disabled: settingsTestIds.usersFilterDisabled,
};

export function UsersFilters({
  filter,
  attention,
  onFilter,
}: {
  filter: UsersFilter;
  attention: number;
  onFilter(filter: UsersFilter): void;
}) {
  const labels: [UsersFilter, string][] = [
    ['all', 'All'],
    ['active', 'Active'],
    ['attention', `Needs attention ${attention}`],
    ['disabled', 'Disabled'],
  ];
  return (
    <div role="group" aria-label="Show" className="flex gap-1">
      {labels.map(([key, label]) => (
        <button
          key={key}
          type="button"
          data-testid={filterIds[key]}
          aria-pressed={filter === key}
          className="btn-ghost aria-pressed:bg-sunken aria-pressed:text-ink"
          onClick={() => onFilter(key)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
