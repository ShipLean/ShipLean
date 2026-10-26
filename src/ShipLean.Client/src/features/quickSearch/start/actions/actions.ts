import { newItemPath, routePath } from '../../../../routes';
import type { Theme } from '../../../../api/me';
import type { QuickSearchRow } from '../../types';

export interface ActionContext {
  /** The project the shell shows; the project actions are left out without one. */
  projectKey: string | undefined;
  /** The saved theme. */
  theme: Theme;
  prefersDark: boolean;
  navigate: (path: string) => void;
  toggleTheme: () => void;
}

type ActionRow = Extract<QuickSearchRow, { kind: 'action' }>;

export const isDarkTheme = (theme: Theme, prefersDark: boolean) =>
  theme === 'os' ? prefersDark : theme === 'dark';

/** The actions quick search offers, in order, narrowed to those whose label contains the query. */
export function buildActions(context: ActionContext, query = ''): ActionRow[] {
  const { projectKey: key, navigate } = context;
  const rows: ActionRow[] = [];
  if (key !== undefined) {
    const create = (kind: 'epic' | 'feature' | 'story' | 'bug'): ActionRow => ({
      kind: 'action',
      id: `create-${kind}`,
      glyph: '+',
      label: `Create ${kind}`,
      detail: `in ${key}`,
      run: () => navigate(newItemPath(key, kind)),
    });
    const goTo = (
      id: string,
      glyph: string,
      label: string,
      route: 'storiesBacklog' | 'storiesBoard' | 'sprints',
    ): ActionRow => ({
      kind: 'action',
      id,
      glyph,
      label,
      detail: key,
      run: () => navigate(routePath(route, { key })),
    });
    rows.push(
      create('epic'),
      create('feature'),
      create('story'),
      create('bug'),
      goTo('go-backlogs', '☰', 'Go to Backlogs', 'storiesBacklog'),
      goTo('go-boards', '▦', 'Go to Boards', 'storiesBoard'),
      goTo('go-sprints', '◷', 'Go to Sprints', 'sprints'),
    );
  }
  const dark = isDarkTheme(context.theme, context.prefersDark);
  rows.push({
    kind: 'action',
    id: 'toggle-theme',
    glyph: '◐',
    label: 'Toggle theme',
    detail: dark ? 'Dark → Light' : 'Light → Dark',
    run: context.toggleTheme,
  });
  const needle = query.trim().toLowerCase();
  return rows.filter((row) => row.label.toLowerCase().includes(needle));
}
