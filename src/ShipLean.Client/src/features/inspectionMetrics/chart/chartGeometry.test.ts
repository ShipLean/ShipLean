import { describe, expect, it } from 'vitest';
import type { InspectionDotDto, InspectionPeriodDto } from '@shiplean/core/api/views';
import fixture from '../../../../../ShipLean.TestSupport/DtoFixtures/views/inspection-metrics-view.json' with { type: 'json' };
import { AXIS_MAX_HOURS, CHART_HEIGHT, leadTimeLayout } from './chartGeometry';

const PERIODS = fixture.periods as unknown as InspectionPeriodDto[];

const withDot = (leadHours: number): InspectionPeriodDto[] => {
  const dot: InspectionDotDto = { ...PERIODS[0].dots[0], taskKey: 'SHIP-1', leadHours };
  return [{ ...PERIODS[0], dots: [dot] }];
};

describe('leadTimeLayout', () => {
  it('leadTimeLayout_WhenDotIsOverTwoDays_ShouldClampItToTheTopEdgeAndFlagIt', () => {
    const layout = leadTimeLayout(withDot(50), 900);
    const [dot] = layout.dots;
    expect(dot.over).toBe(true);
    expect(dot.cy).toBe(layout.top + 2);
  });

  it('leadTimeLayout_WhenDotIsAtTwoDays_ShouldPlotItUnflagged', () => {
    const layout = leadTimeLayout(withDot(AXIS_MAX_HOURS), 900);
    const [dot] = layout.dots;
    expect(dot.over).toBe(false);
    expect(dot.cy).toBe(layout.top);
  });

  it('leadTimeLayout_WhenDotIsUnderTwoDays_ShouldPlotItByItsHours', () => {
    const layout = leadTimeLayout(withDot(24), 900);
    const [dot] = layout.dots;
    expect(dot.over).toBe(false);
    expect(dot.cy).toBeCloseTo((layout.top + layout.baseY) / 2, 5);
  });

  it('leadTimeLayout_WhenNoPeriods_ShouldGiveTheEmptyLayout', () => {
    const layout = leadTimeLayout([], 900);
    expect(layout.empty).toBe(true);
    expect(layout.bars).toEqual([]);
    expect(layout.dots).toEqual([]);
    expect(layout.quarters).toEqual([]);
    expect(layout.height).toBe(CHART_HEIGHT);
  });

  it('leadTimeLayout_WhenFixture_ShouldGiveOneBarPerPeriodAndOneDotPerTask', () => {
    const layout = leadTimeLayout(PERIODS, 900);
    expect(layout.bars.map((b) => b.periodId)).toEqual(PERIODS.map((p) => p.id));
    expect(layout.dots).toHaveLength(PERIODS.reduce((n, p) => n + p.dots.length, 0));
    expect(layout.bars.filter((b) => b.inProgress)).toHaveLength(1);
  });

  it('leadTimeLayout_WhenQuarterChanges_ShouldMarkADividerAtTheChange', () => {
    const layout = leadTimeLayout(PERIODS, 900);
    expect(layout.quarters.map((q) => [q.label, q.divider])).toEqual([
      ['Q3 2026', false],
      ['Q4 2026', true],
    ]);
  });

  it('leadTimeLayout_WhenPeriodHasNoMean_ShouldGiveNoBarTop', () => {
    const empty = {
      ...PERIODS[0],
      leadTime: { ...PERIODS[0].leadTime, tasks: 0, meanHours: null, meanLabel: null },
      dots: [],
    };
    const layout = leadTimeLayout([empty], 900);
    expect(layout.bars[0].top).toBeNull();
  });
});
