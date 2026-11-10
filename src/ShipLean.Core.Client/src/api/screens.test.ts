import { describe, expect, it } from 'vitest';
import backlogRow from '../../../ShipLean.TestSupport/DtoFixtures/screens/backlog-row-needs.json';
import card from '../../../ShipLean.TestSupport/DtoFixtures/screens/card-health.json';
import featureBoardRow from '../../../ShipLean.TestSupport/DtoFixtures/screens/feature-board-row-needs.json';
import retroPoint from '../../../ShipLean.TestSupport/DtoFixtures/screens/retro-point-supported.json';
import workItem from '../../../ShipLean.TestSupport/DtoFixtures/screens/work-item-health.json';
import type { RetroPointDto } from './retros';
import { FEATURE_HEALTHS, FEATURE_RULES } from './screens';
import type { BacklogRowDto, CardDto, FeatureBoardRowDto } from './views';
import type { WorkItemDto } from './workItems';

// Typecheck fails if a fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

const healths: readonly string[] = FEATURE_HEALTHS;

describe('screens fixtures', () => {
  it('types work-item-health, backlog-row-needs, retro-point-supported, feature-board-row-needs and card-health with no extra keys', () => {
    const item = typed<WorkItemDto>()(workItem);
    const row = typed<BacklogRowDto>()(backlogRow);
    const point = typed<RetroPointDto>()(retroPoint);
    const boardRow = typed<FeatureBoardRowDto>()(featureBoardRow);
    const healthCard = typed<CardDto>()(card);

    expect(healths).toContain(item.health);
    expect(item.healthSet).not.toBeNull();
    expect(item.rollups.length).toBeGreaterThan(0);
    expect(item.needsYou).not.toBeNull();
    expect(healths).toContain(row.health);
    expect(row.needsYou).not.toBeNull();
    expect(row.blockedBy.length).toBeGreaterThan(0);
    expect(point.supporters).toHaveLength(2);
    expect(point.supportedByMe).toBe(true);
    expect(boardRow.needsYou).not.toBeNull();
    expect(healths).toContain(healthCard.health);
  });
});

describe('FEATURE_RULES', () => {
  it('keeps the status stale age at the server rule, 3 days', () => {
    expect(FEATURE_RULES.statusStaleAfterDays).toBe(3);
  });
});
