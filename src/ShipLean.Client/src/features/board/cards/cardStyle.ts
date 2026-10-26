import type { CSSProperties } from 'react';
import type { CardAnnotationsJson, StyleRuleJson, Swatch } from '../../../api/persisted';
import type { WorkItemTypeId } from '../../../api/refs';
import type { RollupCountDto } from '../../../api/views';

/** The colour token a swatch names. */
export const swatchVar = (swatch: Swatch) => `var(--color-${swatch})`;

/** The card's tint and left bar for a style rule colour; no colour, no tint. */
export function ruleCardStyle(rule: StyleRuleJson | null): CSSProperties | undefined {
  if (rule === null || rule.colour === null) return undefined;
  const colour = swatchVar(rule.colour);
  return {
    background: `color-mix(in srgb, ${colour} 9%, var(--color-surface))`,
    borderColor: `color-mix(in srgb, ${colour} 40%, var(--color-border))`,
    boxShadow: `inset 3px 0 0 ${colour}`,
  };
}

/** The title's weight, slant and underline for a style rule. */
export function ruleTitleStyle(rule: StyleRuleJson | null): CSSProperties | undefined {
  if (rule === null) return undefined;
  return {
    fontWeight: rule.bold ? 'var(--weight-semibold)' : undefined,
    fontStyle: rule.italic ? 'italic' : undefined,
    textDecoration: rule.underline ? 'underline' : undefined,
  };
}

/** A tag's pill: its swatch tinted, or the neutral surface when the board gives it no colour. */
export function tagStyle(colour: Swatch | null): CSSProperties {
  return {
    background:
      colour === null
        ? 'var(--color-surface-2)'
        : `color-mix(in srgb, ${swatchVar(colour)} 18%, transparent)`,
  };
}

const ANNOTATION_OF: Record<WorkItemTypeId, keyof CardAnnotationsJson | null> = {
  task: 'tasks',
  story: 'stories',
  bug: 'bugs',
  feature: 'features',
  epic: null,
};

/** The roll-ups the board's annotations turn on: one entry per child type, only the types switched on. */
export function visibleRollups(
  rollups: RollupCountDto[],
  annotations: CardAnnotationsJson,
): RollupCountDto[] {
  return rollups.filter((rollup) => {
    const key = ANNOTATION_OF[rollup.type];
    return key !== null && annotations[key];
  });
}

/** `?` for an unestimated card; otherwise the points as they are. */
export const pointsText = (points: number | null) => (points === null ? '?' : String(points));
