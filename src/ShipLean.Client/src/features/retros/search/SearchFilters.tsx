import { RETRO_CATEGORIES } from '../../../api/retros';
import type { RetroDecision } from '../../../api/retros';
import { retroCategoryLabels } from '../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../testIds';
import { SearchBox } from './parts/SearchBox';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

const roles = [
  { value: 'conductor', label: 'Conductors' },
  { value: 'subAgent', label: 'Sub-agents' },
  { value: 'person', label: 'People' },
] as const;

const levels = [
  { value: 'stories', label: 'Stories and bugs' },
  { value: 'features', label: 'Features' },
] as const;

const decisions: { value: RetroDecision; label: string }[] = [
  { value: 'undecided', label: 'Undecided' },
  { value: 'actioned', label: 'Actioned' },
  { value: 'notNeeded', label: 'Not needed' },
];

/** The search box, the sprint, category, theme, level, author and decision filters, No sprint, Unthemed, Recurring only, and Clear. Each writes its query parameter. */
export function SearchFilters({ query, search, change }: RetrosSearchSlotProps) {
  const distinctRetros = new Set(search.rows.map((row) => row.retroId)).size;
  return (
    <div className="flex flex-wrap items-center gap-2 px-4 pt-3">
      <SearchBox value={query.q ?? ''} onWrite={(q) => change({ q })} />
      <select
        data-testid={ids.retrosFilterSprint}
        className="field"
        aria-label="Sprint"
        value={query.sprint ?? ''}
        onChange={(event) => change({ sprint: event.target.value || undefined })}
      >
        <option value="">Any sprint</option>
        {[...search.sprints].reverse().map((sprint) => (
          <option key={sprint.id} value={sprint.id}>
            {sprint.name}
          </option>
        ))}
      </select>
      <select
        data-testid={ids.retrosFilterCategory}
        className="field"
        aria-label="Category"
        value={query.category ?? ''}
        onChange={(event) => change({ category: event.target.value || undefined })}
      >
        <option value="">Any category</option>
        {RETRO_CATEGORIES.map((category) => (
          <option key={category} value={category}>
            {retroCategoryLabels[category]}
          </option>
        ))}
      </select>
      <select
        data-testid={ids.retrosFilterTheme}
        className="field"
        aria-label="Theme"
        value={query.theme ?? ''}
        onChange={(event) => change({ theme: event.target.value || undefined })}
      >
        <option value="">Any theme</option>
        {search.themes.map((theme) => (
          <option key={theme.id} value={theme.id}>
            {theme.name}
          </option>
        ))}
      </select>
      <select
        data-testid={ids.retrosFilterLevel}
        className="field"
        aria-label="Level"
        value={query.level ?? ''}
        onChange={(event) =>
          change({ level: levels.find((level) => level.value === event.target.value)?.value })
        }
      >
        <option value="">Stories, bugs and features</option>
        {levels.map((level) => (
          <option key={level.value} value={level.value}>
            {level.label}
          </option>
        ))}
      </select>
      <select
        data-testid={ids.retrosFilterRole}
        className="field"
        aria-label="Written by"
        value={query.role ?? ''}
        onChange={(event) => change({ role: event.target.value || undefined })}
      >
        <option value="">Anyone</option>
        {roles.map((role) => (
          <option key={role.value} value={role.value}>
            {role.label}
          </option>
        ))}
      </select>
      <select
        data-testid={ids.retrosFilterDecision}
        className="field"
        aria-label="Decision"
        value={query.decision ?? ''}
        onChange={(event) =>
          change({ decision: decisions.find((d) => d.value === event.target.value)?.value })
        }
      >
        <option value="">Any decision</option>
        {decisions.map((decision) => (
          <option key={decision.value} value={decision.value}>
            {decision.label}
          </option>
        ))}
      </select>
      <label className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)]">
        <input
          data-testid={ids.retrosFilterNosprint}
          type="checkbox"
          checked={query.noSprint === true}
          onChange={(event) => change({ noSprint: event.target.checked || undefined })}
        />
        No sprint
      </label>
      <label className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)]">
        <input
          data-testid={ids.retrosFilterUnthemed}
          type="checkbox"
          checked={query.unthemed === true}
          onChange={(event) => change({ unthemed: event.target.checked || undefined })}
        />
        Unthemed
      </label>
      <label className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)]">
        <input
          data-testid={ids.retrosFilterRecurring}
          type="checkbox"
          checked={query.recurring === true}
          onChange={(event) => change({ recurring: event.target.checked || undefined })}
        />
        Recurring themes only
      </label>
      <button
        type="button"
        data-testid={ids.retrosFilterClear}
        className="btn-ghost"
        onClick={() =>
          change({
            q: undefined,
            sprint: undefined,
            category: undefined,
            theme: undefined,
            level: undefined,
            role: undefined,
            recurring: undefined,
            noSprint: undefined,
            decision: undefined,
            unthemed: undefined,
          })
        }
      >
        Clear
      </button>
      <span className="flex-1" />
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        {search.total} point{search.total === 1 ? '' : 's'} in {distinctRetros} retro
        {distinctRetros === 1 ? '' : 's'}
      </span>
    </div>
  );
}
