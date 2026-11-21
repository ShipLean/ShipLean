import { describe, expect, it } from 'vitest';
import { missingPlaceholders, putBack, unknownPlaceholders } from './placeholders';

const names = ['{{task_key}}', '{{wave}}'];

describe('placeholders', () => {
  it('missingPlaceholders_WhenOneRemoved_ShouldListIt', () => {
    expect(missingPlaceholders('Task {{task_key}} only', names)).toEqual(['{{wave}}']);
  });

  it('unknownPlaceholders_WhenTokenNotInList_ShouldListIt', () => {
    expect(unknownPlaceholders('{{task_key}} {{nope}} {{nope}}', names)).toEqual(['{{nope}}']);
  });

  it('putBack_WhenAnchorSurvives_ShouldRestoreTheBuiltInLinePosition', () => {
    const builtIn = 'intro\nWave: {{wave}}\nTask: {{task_key}}\noutro';
    const mine = 'intro\nTask: {{task_key}}\nmy rules\noutro';
    expect(putBack(mine, '{{wave}}', builtIn)).toBe(
      'intro\nWave: {{wave}}\nTask: {{task_key}}\nmy rules\noutro',
    );
  });

  it('putBack_WhenNoAnchor_ShouldAppendAtTheEnd', () => {
    expect(putBack('all new text', '{{wave}}', 'old {{wave}}')).toBe('all new text\nold {{wave}}');
  });

  it('putBack_WhenTextEndsWithNewline_ShouldAppendBeforeIt', () => {
    expect(putBack('new\n', '{{wave}}', 'old {{wave}}')).toBe('new\nold {{wave}}\n');
  });

  it('putBack_WhenTextIsCrlf_ShouldKeepEveryLineEnding', () => {
    const builtIn = 'intro\nWave: {{wave}}\noutro';
    expect(putBack('intro\r\nmine\r\noutro', '{{wave}}', builtIn)).toBe(
      'intro\r\nWave: {{wave}}\r\nmine\r\noutro',
    );
  });

  it('putBack_WhenMixedEndings_ShouldNotRewriteOtherLines', () => {
    expect(putBack('a\r\nb\nc', '{{wave}}', 'a\n{{wave}}')).toBe('a\r\n{{wave}}\r\nb\nc');
  });
});
