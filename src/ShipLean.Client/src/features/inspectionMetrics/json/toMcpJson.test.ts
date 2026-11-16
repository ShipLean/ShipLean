import { describe, expect, it } from 'vitest';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import fixture from '../../../../../ShipLean.TestSupport/DtoFixtures/views/inspection-metrics-view.json' with { type: 'json' };
import { toMcpJson } from './toMcpJson';

const VIEW = fixture as unknown as InspectionMetricsViewDto;

const keysOf = (value: unknown): string[] =>
  Array.isArray(value)
    ? value.flatMap(keysOf)
    : value !== null && typeof value === 'object'
      ? Object.entries(value).flatMap(([k, v]) => [k, ...keysOf(v)])
      : [];

describe('toMcpJson', () => {
  it('ShouldSnakeCaseEveryKey', () => {
    const json = toMcpJson(VIEW);
    expect(keysOf(json).filter((k) => /[A-Z]/.test(k))).toEqual([]);
    expect(json).toHaveProperty('headline.finished.lead_time.change_pct_vs_previous');
    expect(json).toHaveProperty('period_detail');
  });

  it('ShouldSnakeCaseEnumValuesButNotFreeText', () => {
    const json = JSON.stringify(
      toMcpJson({
        ...VIEW,
        range: 'lastQuarter',
        leftOut: [{ ...VIEW.leftOut[0], reason: 'noStart text' }],
      }),
    );
    expect(json).toContain('"range":"last_quarter"');
    expect(json).toContain('"ended_by":"last_commit"');
    expect(json).not.toContain('lastCommit');
    expect(json).toContain('"reason":"noStart text"');
  });

  it('ShouldSnakeCaseTheSelectedTasksSegmentsAndEvents', () => {
    const json = JSON.stringify(toMcpJson(VIEW));
    expect(json).toContain('"kind":"unresolved_tail"');
    expect(json).toContain('"kind":"review_changes_requested"');
    expect(json).not.toMatch(/"kind":"[a-z]+[A-Z]/);
  });
});
