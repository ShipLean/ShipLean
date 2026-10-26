import type { QuickSearchRow } from '../types';

// TODO: replaced by the real actions in useActionSource.
export const fakeActionRows: readonly QuickSearchRow[] = [
  {
    kind: 'action',
    id: 'create-story',
    glyph: '+',
    label: 'Create story',
    detail: 'in SHIP',
    run: () => undefined,
  },
  {
    kind: 'action',
    id: 'toggle-theme',
    glyph: '◐',
    label: 'Toggle theme',
    detail: null,
    run: () => undefined,
  },
];
