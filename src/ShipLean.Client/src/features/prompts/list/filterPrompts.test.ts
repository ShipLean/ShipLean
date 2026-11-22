import { describe, expect, it } from 'vitest';
import { adminPage } from '../ct/promptsData';
import { filterPrompts } from './filterPrompts';

const keys = (query: string, filter: 'all' | 'custom' = 'all') =>
  filterPrompts(adminPage.stages, query, filter).flatMap((s) => s.prompts.map((p) => p.key));

describe('filterPrompts', () => {
  it('keeps every prompt when nothing is asked', () => {
    expect(keys('')).toHaveLength(12);
  });

  it('matches the name, case-insensitively', () => {
    expect(keys('PLAN REVIEWER')).toEqual(['planReviewer']);
  });

  it('matches the file', () => {
    expect(keys('code_reviewer.md')).toEqual(['codeReviewer']);
  });

  it('matches the built-in text when no custom version is in use', () => {
    expect(keys('built-in text for fix')).toEqual(['fix']);
  });

  it('matches the custom text in use, not a custom version that is kept aside', () => {
    expect(keys('our own line')).toEqual(['guide']);
    expect(keys('write a proposal for this issue')).toEqual([]);
  });

  it('matches a placeholder with or without its braces', () => {
    expect(keys('task_key')).toContain('brief');
    expect(keys('{{sprint_name}}')).toEqual(['sprintDemo']);
  });

  it('keeps customised prompts under the Custom filter', () => {
    expect(keys('', 'custom')).toEqual(['guide', 'brief']);
  });

  it('returns nothing when no prompt matches', () => {
    expect(filterPrompts(adminPage.stages, 'zzz-no-such-prompt', 'all')).toEqual([]);
  });

  it('leaves out a stage whose prompts all drop out', () => {
    expect(filterPrompts(adminPage.stages, 'sprint', 'all').map((s) => s.id)).toContain(
      'sprintDemo',
    );
    expect(filterPrompts(adminPage.stages, 'fix.md', 'all').map((s) => s.id)).toEqual(['review']);
  });
});
