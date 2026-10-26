import { describe, expect, it, vi } from 'vitest';
import { buildActions, type ActionContext } from './actions';

const context = (over: Partial<ActionContext> = {}): ActionContext => ({
  projectKey: 'SHIP',
  theme: 'light',
  prefersDark: false,
  navigate: vi.fn(),
  toggleTheme: vi.fn(),
  ...over,
});

describe('buildActions', () => {
  it('buildActions_WhenProjectOpen_ShouldListAllEightInOrder', () => {
    const navigate = vi.fn();
    const rows = buildActions(context({ navigate }));
    expect(rows.map((r) => r.id)).toEqual([
      'create-epic',
      'create-feature',
      'create-story',
      'create-bug',
      'go-backlogs',
      'go-boards',
      'go-sprints',
      'toggle-theme',
    ]);
    expect(rows[0].detail).toBe('in SHIP');
    expect(rows[4].detail).toBe('SHIP');
    rows[3].run();
    expect(navigate).toHaveBeenCalledWith('/p/SHIP/backlogs/stories?new=bug');
    const labels = rows.map((r) => r.label);
    expect(labels).not.toContain('Create task');
    expect(labels).not.toContain('Create work item');
  });

  it('buildActions_WhenNoProject_ShouldOfferOnlyToggleTheme', () => {
    const rows = buildActions(context({ projectKey: undefined }));
    expect(rows.map((r) => r.id)).toEqual(['toggle-theme']);
  });

  it('buildActions_WhenQueryTyped_ShouldFilterByLabel', () => {
    // A filter on id or detail instead of label would also keep "Go to Boards" for "ship".
    expect(buildActions(context(), '  GO TO ').map((r) => r.id)).toEqual([
      'go-backlogs',
      'go-boards',
      'go-sprints',
    ]);
    expect(buildActions(context(), 'ship')).toEqual([]);
  });

  it('buildActions_WhenThemeIsOsAndOsIsDark_ShouldOfferLight', () => {
    const toggle = (over: Partial<ActionContext>) =>
      buildActions(context(over)).find((r) => r.id === 'toggle-theme')?.detail;
    expect(toggle({ theme: 'os', prefersDark: true })).toBe('Dark → Light');
    expect(toggle({ theme: 'os', prefersDark: false })).toBe('Light → Dark');
    expect(toggle({ theme: 'light', prefersDark: true })).toBe('Light → Dark');
  });
});
