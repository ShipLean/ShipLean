import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import type { RetroSearchDto } from '@shiplean/core/api/retros';
import type { RetroSearchQuery } from '@shiplean/core/api/retrosApi';
import { retroCategoryLabels } from '../../../retroShared/retroCategories';
import { decisions, levels, roles } from '../SearchFilters';

/** A patch that clears every filter but leaves the search text. */
export const CLEAR_FILTERS: Partial<RetroSearchQuery> = {
  sprint: undefined,
  category: undefined,
  theme: undefined,
  level: undefined,
  role: undefined,
  recurring: undefined,
  noSprint: undefined,
  decision: undefined,
  unthemed: undefined,
};

/** A patch that clears the search text too. */
export const CLEAR_SEARCH_AND_FILTERS: Partial<RetroSearchQuery> = {
  ...CLEAR_FILTERS,
  q: undefined,
};

export interface ActiveFilter {
  key: string;
  label: string;
  /** The patch that removes just this filter. */
  clear: Partial<RetroSearchQuery>;
}

const labelOf = (options: readonly { value: string; label: string }[], value: string) =>
  options.find((option) => option.value === value)?.label ?? value;

/** One entry per filter the address holds, in the order the Filter sheet lists them (the search text is not a filter). */
export function activeFilters(query: RetroSearchQuery, search: RetroSearchDto): ActiveFilter[] {
  const active: ActiveFilter[] = [];
  if (query.category) {
    const category = RETRO_CATEGORIES.find((c) => c === query.category);
    active.push({
      key: 'category',
      label: category ? retroCategoryLabels[category] : query.category,
      clear: { category: undefined },
    });
  }
  if (query.level)
    active.push({ key: 'level', label: labelOf(levels, query.level), clear: { level: undefined } });
  if (query.role)
    active.push({ key: 'role', label: labelOf(roles, query.role), clear: { role: undefined } });
  if (query.decision)
    active.push({
      key: 'decision',
      label: labelOf(decisions, query.decision),
      clear: { decision: undefined },
    });
  if (query.sprint) {
    const sprint = search.sprints.find((s) => s.id === query.sprint);
    active.push({ key: 'sprint', label: sprint?.name ?? 'Sprint', clear: { sprint: undefined } });
  }
  if (query.theme) {
    const theme = search.themes.find((t) => t.id === query.theme);
    active.push({
      key: 'theme',
      label: `# ${theme?.name ?? 'Theme'}`,
      clear: { theme: undefined },
    });
  }
  if (query.noSprint)
    active.push({ key: 'noSprint', label: 'No sprint', clear: { noSprint: undefined } });
  if (query.unthemed)
    active.push({ key: 'unthemed', label: 'Unthemed', clear: { unthemed: undefined } });
  if (query.recurring)
    active.push({
      key: 'recurring',
      label: 'Recurring themes only',
      clear: { recurring: undefined },
    });
  return active;
}
