import { describe, expect, it } from 'vitest';
import { pageInView, pageWidth, scrollTarget } from './pagerScroll';

describe('pagerScroll', () => {
  it('pageInView_WhenScrolledToTheSecondPage_ShouldBeIt', () => {
    expect(pageInView(340, 340, 8, 4)).toBe(1);
  });

  it('pageInView_WhenBetweenPages_ShouldBeTheNearest', () => {
    expect(pageInView(100, 340, 8, 4)).toBe(0);
    expect(pageInView(200, 340, 8, 4)).toBe(1);
    expect(pageInView(5000, 340, 8, 4)).toBe(3);
  });

  it('pageWidth_WhenOnAPhone_ShouldBeAtMost340', () => {
    // The track's content width: 366px at 390px (full bleed, 12px padding each side).
    expect(pageWidth(366)).toBe(340);
    expect(pageWidth(351)).toBe(325);
    expect(pageWidth(744)).toBe(340);
  });

  it('scrollTarget_WhenPastTheEnd_ShouldStopAtTheMostTheTrackScrolls', () => {
    expect(scrollTarget(2, 340, 8, 292)).toBe(292);
    expect(scrollTarget(1, 340, 8, 1000)).toBe(348);
  });
});
