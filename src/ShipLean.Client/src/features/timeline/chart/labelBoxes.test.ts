import { describe, expect, it } from 'vitest';
import type { Mark } from './timelineGeometry';
import {
  DURATION_LABEL_FONT_SIZE,
  DURATION_LABEL_GAP,
  durationLabelBox,
  markBox,
  textWidth,
} from './labelBoxes';

const bar = (over: Partial<Mark> = {}): Mark => ({
  key: 'SHIP-1',
  kind: 'bar',
  row: 0,
  box: { key: 'SHIP-1', x0: 100, x1: 200, y: 48, height: 14 },
  reviews: [],
  reviewLine: null,
  durationLabel: '1.2 d',
  ...over,
});
const dots = (...xs: number[]): Mark['reviews'] =>
  xs.map((x) => ({ x, verdict: 'approved' as const, atLabel: 'Mon 5 Oct, 10:00' }));

describe('labelBoxes', () => {
  it('DurationLabelBox_ShouldStartSixPxAfterTheBarEnd', () => {
    const box = durationLabelBox(bar(), '1.2 d')!;
    expect(DURATION_LABEL_GAP).toBe(6);
    expect(box.x0).toBe(206);
  });

  it('DurationLabelBox_WhenReviewDotIsLater_ShouldStartAfterTheDotPlusItsRadius', () => {
    const box = durationLabelBox(bar({ reviews: dots(230, 260) }), '1.2 d')!;
    expect(box.x0).toBe(260 + 4 + 6);
  });

  it('DurationLabelBox_WhenReviewDotIsInsideTheBar_ShouldStartAfterTheBarEnd', () => {
    const box = durationLabelBox(bar({ reviews: dots(150) }), '1.2 d')!;
    expect(box.x0).toBe(206);
  });

  it('DurationLabelBox_ShouldBeCentredOnTheMarkAtTenPx', () => {
    const box = durationLabelBox(bar(), '1.2 d')!;
    expect(DURATION_LABEL_FONT_SIZE).toBe(10);
    expect((box.y0 + box.y1) / 2).toBe(48 + 7);
    expect(box.y1 - box.y0).toBe(10);
  });

  it('DurationLabelBox_ShouldGrowWithTheText', () => {
    const short = durationLabelBox(bar(), '2 h')!;
    const long = durationLabelBox(bar(), '12.5 d so far')!;
    expect(long.x1 - long.x0).toBeGreaterThan(short.x1 - short.x0);
    expect(short.x1 - short.x0).toBe(textWidth('2 h', 10));
  });

  it('DurationLabelBox_WhenBracket_ShouldSitAfterItsEnd', () => {
    const bracket = bar({
      kind: 'bracket',
      box: { key: 'SHIP-1', x0: 10, x1: 90, y: 51, height: 8 },
    });
    const box = durationLabelBox(bracket, '3 d')!;
    expect(box.x0).toBe(96);
    expect((box.y0 + box.y1) / 2).toBe(55);
  });

  it('DurationLabelBox_WhenDiamond_ShouldBeNull', () => {
    const diamond = bar({
      kind: 'diamond',
      box: { key: 'SHIP-1', x0: 50, x1: 50, y: 48, height: 14 },
    });
    expect(durationLabelBox(diamond, '1 d')).toBeNull();
  });

  it('TextWidth_ShouldBeAFixedEstimatePerCharacter', () => {
    expect(textWidth('', 10)).toBe(0);
    expect(textWidth('abcd', 10)).toBe(2 * textWidth('ab', 10));
    expect(textWidth('abcd', 20)).toBe(2 * textWidth('abcd', 10));
  });

  it('MarkBox_ShouldIncludeReviewDots', () => {
    const box = markBox(bar({ reviews: dots(260) }));
    expect(box).toEqual({ x0: 100, x1: 264, y0: 48, y1: 62 });
  });

  it('MarkBox_WhenNoReviews_ShouldBeTheBar', () => {
    expect(markBox(bar())).toEqual({ x0: 100, x1: 200, y0: 48, y1: 62 });
  });

  it('MarkBox_WhenNarrowerThanMinimum_ShouldBeTheDrawnWidth', () => {
    const box = markBox(bar({ box: { key: 'SHIP-1', x0: 100, x1: 100.5, y: 48, height: 14 } }));
    expect(box.x1).toBe(103);
  });

  it('MarkBox_WhenDiamond_ShouldBeItsSquare', () => {
    const diamond = bar({
      kind: 'diamond',
      box: { key: 'SHIP-1', x0: 50, x1: 50, y: 48, height: 14 },
    });
    expect(markBox(diamond)).toEqual({ x0: 43, x1: 57, y0: 48, y1: 62 });
  });
});
