import { describe, expect, it } from 'vitest';
import type { StyleRuleJson } from '../../../api/persisted';
import { pointsText, ruleCardStyle, ruleTitleStyle, tagStyle, visibleRollups } from './cardStyle';

const rule = (overrides: Partial<StyleRuleJson>): StyleRuleJson => ({
  id: 'r1',
  name: 'Blocked',
  enabled: true,
  colour: 'danger',
  bold: false,
  italic: false,
  underline: false,
  criteria: { match: 'all', clauses: [] },
  ...overrides,
});

describe('cardStyle', () => {
  it('ruleCardStyle_WhenRuleHasColour_ShouldTintFromThatToken', () => {
    expect(ruleCardStyle(rule({}))).toEqual({
      background: 'color-mix(in srgb, var(--color-danger) 9%, var(--paper, var(--color-surface)))',
      borderColor:
        'color-mix(in srgb, var(--color-danger) 40%, var(--paper-edge, var(--color-border)))',
      boxShadow: 'inset 3px 0 0 var(--color-danger), var(--paper-shadow)',
    });
    expect(ruleCardStyle(rule({ colour: 'purple' }))?.background).toContain('var(--color-purple)');
  });

  it('ruleCardStyle_WhenTintPercentGiven_ShouldUseItForTheBackground', () => {
    expect(ruleCardStyle(rule({}), 12)?.background).toBe(
      'color-mix(in srgb, var(--color-danger) 12%, var(--paper, var(--color-surface)))',
    );
  });

  it('ruleCardStyle_WhenNoRuleOrNoColour_ShouldNotTint', () => {
    expect(ruleCardStyle(null)).toBeUndefined();
    expect(ruleCardStyle(rule({ colour: null }))).toBeUndefined();
  });

  it('ruleTitleStyle_WhenBoldItalicUnderline_ShouldSetEach', () => {
    const style = ruleTitleStyle(rule({ bold: true, italic: true, underline: true }));
    expect(style).toMatchObject({ fontStyle: 'italic', textDecoration: 'underline' });
    expect(style?.fontWeight).toBe('var(--weight-semibold)');
  });

  it('tagStyle_WhenNoColour_ShouldUseTheNeutralSurface', () => {
    expect(tagStyle(null).background).toBe('var(--color-surface-2)');
    expect(tagStyle('ok').background).toContain('var(--color-ok)');
  });

  it('visibleRollups_WhenAnnotationOff_ShouldDropThatType', () => {
    const rollups = [
      { type: 'story', open: 1, closed: 2 },
      { type: 'task', open: 1, closed: 0 },
      { type: 'feature', open: 0, closed: 1 },
    ] as const;
    const shown = visibleRollups([...rollups], {
      tasks: false,
      stories: true,
      bugs: false,
      features: true,
    });
    expect(shown.map((r) => r.type)).toEqual(['story', 'feature']);
  });

  it('pointsText_WhenUnestimated_ShouldBeQuestionMark', () => {
    expect(pointsText(null)).toBe('?');
    expect(pointsText(0)).toBe('0');
    expect(pointsText(5)).toBe('5');
  });
});
