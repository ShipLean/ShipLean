import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import type { RetroSearchQuery } from '@shiplean/core/api/retrosApi';
import { Sheet } from '../../../../touch/Sheet';
import { retroCategoryLabels } from '../../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../../testIds';
import { decisions, levels, roles } from '../SearchFilters';
import type { RetrosSearchSlotProps } from '../SearchSlotProps';
import { CLEAR_FILTERS } from './searchFilters';

type Option = { value: string; label: string };

function Choices({
  title,
  filter,
  options,
  current,
  onPick,
}: {
  title: string;
  filter: string;
  options: readonly Option[];
  current: string | undefined;
  onPick: (value: string | undefined) => void;
}) {
  return (
    <div role="group" aria-label={title} className="grid gap-1.5">
      <span className="text-[length:var(--text-sm)] font-medium text-ink-muted">{title}</span>
      <div className="flex flex-wrap gap-1.5">
        {options.map((option) => (
          <button
            key={option.value}
            type="button"
            data-testid={ids.retrosFilterChoice}
            data-filter={filter}
            data-value={option.value}
            aria-pressed={current === option.value}
            className="min-h-[var(--touch-target)] cursor-pointer rounded-full border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink aria-pressed:border-signal aria-pressed:bg-signal-soft aria-pressed:font-semibold"
            onClick={() => onPick(current === option.value ? undefined : option.value)}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  );
}

function Switch({
  testId,
  label,
  on,
  onChange,
}: {
  testId: string;
  label: string;
  on: boolean;
  onChange: (on: boolean) => void;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      data-testid={testId}
      className="flex min-h-[var(--touch-target)] w-full cursor-pointer items-center justify-between gap-3 text-left text-[length:var(--text-base)] text-ink"
      onClick={() => onChange(!on)}
    >
      <span>{label}</span>
      <span
        aria-hidden="true"
        className={`relative h-6 w-10 flex-none rounded-full border ${on ? 'border-signal bg-signal' : 'border-edge bg-sunken'}`}
      >
        <span
          className={`absolute top-0.5 size-5 rounded-full bg-panel shadow-sm transition-[left] ${on ? 'left-[18px]' : 'left-0.5'}`}
        />
      </span>
    </button>
  );
}

/**
 * The Filter sheet (D3): choice chips for Category, Level, Written by and Decision, native selects for Sprint and Theme,
 * switches for No sprint, Unthemed and Recurring themes only. Each change goes to the address at once; the foot has Clear
 * (the filters, not the search text) and Show n points, which closes the sheet.
 */
export function FilterSheet({
  open,
  onClose,
  query,
  search,
  change,
}: RetrosSearchSlotProps & { open: boolean; onClose: () => void }) {
  const set = (patch: Partial<RetroSearchQuery>) => change(patch);
  return (
    <Sheet
      open={open}
      title="Filter"
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            data-testid={ids.retrosFilterClear}
            className="btn-ghost min-h-[var(--touch-target)]"
            onClick={() => change(CLEAR_FILTERS)}
          >
            Clear
          </button>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.retrosFilterShow}
            className="btn-signal min-h-[var(--touch-target)]"
            onClick={onClose}
          >
            Show {search.total} point{search.total === 1 ? '' : 's'}
          </button>
        </>
      }
    >
      <div className="grid gap-4 pt-1">
        <Choices
          title="Category"
          filter="category"
          options={RETRO_CATEGORIES.map((value) => ({ value, label: retroCategoryLabels[value] }))}
          current={query.category}
          onPick={(category) => set({ category })}
        />
        <Choices
          title="Level"
          filter="level"
          options={levels}
          current={query.level}
          onPick={(level) => set({ level: levels.find((l) => l.value === level)?.value })}
        />
        <Choices
          title="Written by"
          filter="role"
          options={roles}
          current={query.role}
          onPick={(role) => set({ role })}
        />
        <Choices
          title="Decision"
          filter="decision"
          options={decisions}
          current={query.decision}
          onPick={(decision) =>
            set({ decision: decisions.find((d) => d.value === decision)?.value })
          }
        />
        <label className="grid gap-1.5 text-[length:var(--text-sm)] font-medium text-ink-muted">
          Sprint
          <select
            data-testid={ids.retrosFilterSprint}
            className="field"
            value={query.sprint ?? ''}
            onChange={(event) => set({ sprint: event.target.value || undefined })}
          >
            <option value="">Any sprint</option>
            {[...search.sprints].reverse().map((sprint) => (
              <option key={sprint.id} value={sprint.id}>
                {sprint.name}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1.5 text-[length:var(--text-sm)] font-medium text-ink-muted">
          Theme
          <select
            data-testid={ids.retrosFilterTheme}
            className="field"
            value={query.theme ?? ''}
            onChange={(event) => set({ theme: event.target.value || undefined })}
          >
            <option value="">Any theme</option>
            {search.themes.map((theme) => (
              <option key={theme.id} value={theme.id}>
                {theme.name}
              </option>
            ))}
          </select>
        </label>
        <div className="grid">
          <Switch
            testId={ids.retrosFilterNosprint}
            label="No sprint"
            on={query.noSprint === true}
            onChange={(on) => set({ noSprint: on || undefined })}
          />
          <Switch
            testId={ids.retrosFilterUnthemed}
            label="Unthemed"
            on={query.unthemed === true}
            onChange={(on) => set({ unthemed: on || undefined })}
          />
          <Switch
            testId={ids.retrosFilterRecurring}
            label="Recurring themes only"
            on={query.recurring === true}
            onChange={(on) => set({ recurring: on || undefined })}
          />
        </div>
      </div>
    </Sheet>
  );
}
