import { describe, expect, it } from 'vitest';
import { checkPrompt } from './promptChecks';

const facts = { placeholders: ['{{task_key}}'] };

describe('checkPrompt', () => {
  it('checkPrompt_WhenTagUnclosed_ShouldBlockSave', () => {
    const result = checkPrompt('{{task_key}}\n<cursor>\nx', facts);
    expect(result.canSave).toBe(false);
    expect(result.problems.map((p) => p.kind)).toEqual(['unclosed']);
  });

  it('checkPrompt_WhenTagMisspelt_ShouldWarnWithSuggestionAndAllowSave', () => {
    const result = checkPrompt('{{task_key}}\n<claudecode>\nx\n</claudecode>', facts);
    expect(result.canSave).toBe(true);
    expect(result.problems[0]).toMatchObject({
      kind: 'unknownTag',
      suggestion: 'claude-code',
      line: 2,
    });
  });

  it('checkPrompt_WhenPlaceholderRemoved_ShouldWarnAndAllowSave', () => {
    const result = checkPrompt('nothing here', facts);
    expect(result.canSave).toBe(true);
    expect(result.problems).toMatchObject([
      { kind: 'placeholderMissing', placeholder: '{{task_key}}' },
    ]);
  });

  it('checkPrompt_WhenPlaceholderUnknown_ShouldWarnAndAllowSave', () => {
    const result = checkPrompt('{{task_key}} {{other}}', facts);
    expect(result.canSave).toBe(true);
    expect(result.problems).toMatchObject([
      { kind: 'placeholderUnknown', placeholder: '{{other}}' },
    ]);
  });

  it('checkPrompt_WhenNested_ShouldBlockSave', () => {
    expect(
      checkPrompt('{{task_key}}\n<cursor>\n<codex-cli>\n</codex-cli>\n</cursor>', facts).canSave,
    ).toBe(false);
  });

  it('checkPrompt_WhenClean_ShouldHaveNoProblems', () => {
    expect(checkPrompt('{{task_key}}', facts)).toEqual({ canSave: true, problems: [] });
  });
});
